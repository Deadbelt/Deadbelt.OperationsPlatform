using System.Text;
using System.Text.RegularExpressions;

namespace Deadbelt.Infrastructure.Doctor;

internal static partial class DayZPowerShellStartupParser
{
    private static readonly HashSet<string> SupportedArguments = new(
        [
            "config",
            "profiles",
            "mission",
            "port",
            "mod",
            "serverMod",
            "storage",
            "BEpath"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static PowerShellStartupParseResult Parse(
        string content,
        string startupFilePath,
        string dayZRootPath)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(startupFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dayZRootPath);

        var limitations = new List<string>();
        var sanitized = RemoveCommentsAndContinuations(content, limitations);
        var variables = new Dictionary<string, StaticValue>(
            StringComparer.OrdinalIgnoreCase);
        var commands = new List<DayZLaunchCommand>();
        var startupDirectory = Path.GetDirectoryName(startupFilePath)
            ?? dayZRootPath;
        variables["PSScriptRoot"] = StaticValue.FromScalar(startupDirectory);
        ReadParameterDefaults(sanitized, variables, limitations);
        var nextScopeId = 0;
        var statements = ExpandStatements(
            SplitStatements(sanitized, limitations),
            limitations,
            ref nextScopeId);
        var conditionalScopes =
            new Dictionary<int, Dictionary<string, StaticValue>>();

        foreach (var statement in statements)
        {
            var candidate = statement.Text.Trim();
            var statementVariables = variables;

            if (statement.ScopeId != 0)
            {
                if (!conditionalScopes.TryGetValue(
                        statement.ScopeId,
                        out statementVariables))
                {
                    statementVariables = new Dictionary<string, StaticValue>(
                        variables,
                        StringComparer.OrdinalIgnoreCase);
                    conditionalScopes[statement.ScopeId] = statementVariables;
                }
            }

            if (candidate.Length == 0)
                continue;

            if (TryReadAssignment(
                    candidate,
                    statementVariables,
                    limitations,
                    statement.IsConditional))
            {
                continue;
            }

            if (IsUnsupportedBoundary(candidate, limitations))
                continue;

            var command = candidate.StartsWith(
                    "Start-Process",
                    StringComparison.OrdinalIgnoreCase)
                ? TryParseStartProcess(
                    candidate,
                    statementVariables,
                    startupDirectory,
                    dayZRootPath,
                    limitations)
                : TryParseDirectLaunch(
                    candidate,
                    statementVariables,
                    startupDirectory,
                    dayZRootPath,
                    limitations);

            if (command is not null)
                commands.Add(command);
        }

        var distinctCommands = commands
            .GroupBy(CreateCommandSignature, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (distinctCommands.Length > 1)
        {
            limitations.Add(
                "More than one DayZ server launch command was found in the PowerShell script.");
        }

        return new PowerShellStartupParseResult(
            distinctCommands,
            limitations);
    }

    private static void ReadParameterDefaults(
        string content,
        Dictionary<string, StaticValue> variables,
        ICollection<string> limitations)
    {
        var match = ParameterBlockPattern().Match(content);

        if (!match.Success)
            return;

        var defaultsFound = false;

        foreach (var declaration in SplitCollection(match.Groups["body"].Value))
        {
            var parameter = ParameterDefaultPattern().Match(declaration);

            if (!parameter.Success)
            {
                limitations.Add(
                    "A PowerShell parameter declaration could not be resolved statically.");
                continue;
            }

            defaultsFound = true;
            var name = parameter.Groups["name"].Value;
            var expression = parameter.Groups["expression"].Value.Trim();

            if (IsSensitiveName(name))
            {
                limitations.Add(
                    "A sensitive PowerShell parameter default was intentionally excluded.");
                continue;
            }

            if (TryResolveExpression(
                    expression,
                    variables,
                    out var value,
                    limitations))
            {
                variables[name] = value;
            }
            else
            {
                limitations.Add(
                    $"{DescribeVariable(name)} has no statically resolvable default.");
            }
        }

        if (defaultsFound)
        {
            limitations.Add(
                "Declared PowerShell parameter defaults may be overridden at runtime.");
        }
    }

    private static bool TryReadAssignment(
        string statement,
        Dictionary<string, StaticValue> variables,
        ICollection<string> limitations,
        bool isConditional)
    {
        var match = AssignmentPattern().Match(statement);

        if (!match.Success)
        {
            if (statement.TrimStart().StartsWith('$')
                && statement.Contains('='))
            {
                limitations.Add(
                    "A PowerShell variable assignment is malformed.");
                return true;
            }

            return false;
        }

        var name = match.Groups["name"].Value;
        var operation = match.Groups["operation"].Value;
        var expression = match.Groups["expression"].Value.Trim();

        if (IsSensitiveName(name))
        {
            limitations.Add(
                "A sensitive variable assignment was intentionally excluded from static startup analysis.");
            return true;
        }

        if (!TryResolveExpression(
                expression,
                variables,
                out var value,
                limitations))
        {
            limitations.Add(
                $"{DescribeVariable(name)} could not be resolved statically.");
            return true;
        }

        if (operation == "+=")
        {
            if (!variables.TryGetValue(name, out var existing)
                || !existing.TryAppend(value, out var combined))
            {
                limitations.Add(
                    $"{DescribeVariable(name)} could not be extended statically.");
                return true;
            }

            variables[name] = combined;

            if (isConditional)
            {
                limitations.Add(
                    "A conditional PowerShell collection addition was recovered only within its runtime branch.");
            }

            return true;
        }

        variables[name] = value;

        if (isConditional)
        {
            limitations.Add(
                "A variable assignment inside runtime control flow was recovered only for partial launch analysis.");
        }

        return true;
    }

    private static bool TryResolveExpression(
        string expression,
        IReadOnlyDictionary<string, StaticValue> variables,
        out StaticValue value,
        ICollection<string> limitations)
    {
        value = StaticValue.Empty;
        var trimmed = expression.Trim();

        if (trimmed.StartsWith("@{", StringComparison.Ordinal)
            && trimmed.EndsWith('}'))
        {
            return TryResolveHashtable(
                trimmed[2..^1],
                variables,
                out value,
                limitations);
        }

        if (trimmed.Contains("$(", StringComparison.Ordinal)
            || trimmed.Contains("@{", StringComparison.Ordinal)
            || trimmed.Contains('{')
            || trimmed.Contains('}'))
        {
            limitations.Add(
                "PowerShell command substitutions, hashtables, and script blocks are not statically evaluated.");
            return false;
        }

        if (trimmed.StartsWith("@(", StringComparison.Ordinal)
            && trimmed.EndsWith(')'))
        {
            var elements = SplitCollection(trimmed[2..^1]);
            var resolved = new List<string>();

            foreach (var element in elements)
            {
                if (!TryResolveScalar(
                        element,
                        variables,
                        out var item,
                        limitations))
                {
                    return false;
                }

                resolved.Add(item);
            }

            value = StaticValue.FromArray(resolved);
            return true;
        }

        var commaSeparated = SplitCollection(trimmed);

        if (commaSeparated.Count > 1)
        {
            var resolved = new List<string>();

            foreach (var element in commaSeparated)
            {
                if (!TryResolveScalar(
                        element,
                        variables,
                        out var item,
                        limitations))
                {
                    return false;
                }

                resolved.Add(item);
            }

            value = StaticValue.FromArray(resolved);
            return true;
        }

        var tokens = Tokenize(trimmed, limitations);

        if (tokens.Count == 3
            && tokens[0].IsUnquotedValue("Join-Path")
            && TryResolveToken(tokens[1], variables, out var parent, limitations)
            && TryResolveToken(tokens[2], variables, out var child, limitations))
        {
            try
            {
                value = StaticValue.FromScalar(Path.Combine(parent, child));
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                    or NotSupportedException
                    or PathTooLongException)
            {
                limitations.Add(
                    "A supported Join-Path expression contains an invalid path.");
                return false;
            }
        }

        if (tokens.Count != 1)
        {
            limitations.Add(
                "A PowerShell assignment uses an unsupported runtime expression.");
            return false;
        }

        if (tokens[0].Kind == PowerShellTokenKind.Variable
            && variables.TryGetValue(tokens[0].Value, out var existing))
        {
            value = existing;
            return true;
        }

        if (!TryResolveToken(
                tokens[0],
                variables,
                out var scalar,
                limitations))
        {
            return false;
        }

        value = StaticValue.FromScalar(scalar);
        return true;
    }

    private static bool TryResolveHashtable(
        string content,
        IReadOnlyDictionary<string, StaticValue> variables,
        out StaticValue value,
        ICollection<string> limitations)
    {
        var members = new Dictionary<string, StaticValue>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in SplitHashtableEntries(content))
        {
            var separator = entry.IndexOf('=');

            if (separator <= 0)
            {
                value = StaticValue.Empty;
                limitations.Add(
                    "A PowerShell splat has a malformed static member.");
                return false;
            }

            var name = entry[..separator].Trim();
            var expression = entry[(separator + 1)..].Trim();

            if (!IsVariableName(name)
                || IsSensitiveName(name)
                || !TryResolveExpression(
                    expression,
                    variables,
                    out var member,
                    limitations))
            {
                value = StaticValue.Empty;
                limitations.Add(
                    "A PowerShell splat contains an unsupported or unresolved member.");
                return false;
            }

            members[name] = member;
        }

        if (members.Count == 0)
        {
            value = StaticValue.Empty;
            limitations.Add("An empty PowerShell splat was ignored.");
            return false;
        }

        value = StaticValue.FromMembers(members);
        return true;
    }

    private static DayZLaunchCommand? TryParseDirectLaunch(
        string statement,
        IReadOnlyDictionary<string, StaticValue> variables,
        string startupDirectory,
        string dayZRootPath,
        ICollection<string> limitations)
    {
        var tokens = Tokenize(statement, limitations);

        if (tokens.Count == 0)
            return null;

        var position = tokens[0].IsUnquotedValue("&")
            ? 1
            : 0;

        if (position >= tokens.Count)
        {
            limitations.Add(
                "A PowerShell call operator does not have a static executable.");
            return null;
        }

        if (!TryResolveToken(
                tokens[position],
                variables,
                out var executable,
                limitations,
                reportUnresolvedVariable: tokens[0].IsUnquotedValue("&")))
        {
            return null;
        }

        if (!IsDayZExecutable(executable))
            return null;

        var arguments = new List<string>();

        foreach (var token in tokens.Skip(position + 1))
        {
            if (token.Kind == PowerShellTokenKind.Comma)
                continue;

            if (token.Kind == PowerShellTokenKind.Variable
                && variables.TryGetValue(token.Value, out var variable)
                && variable.Items is not null)
            {
                arguments.AddRange(variable.Items);
                continue;
            }

            if (token.Kind == PowerShellTokenKind.Splat)
            {
                if (!variables.TryGetValue(token.Value, out var splat)
                    || splat.Items is null)
                {
                    limitations.Add(
                        $"{DescribeVariable(token.Value, isSplat: true)} could not be resolved statically.");
                    continue;
                }

                arguments.AddRange(splat.Items);
                continue;
            }

            if (!TryResolveToken(
                    token,
                    variables,
                    out var argument,
                    limitations,
                    reportUnresolvedVariable: true))
            {
                continue;
            }

            arguments.Add(argument);
        }

        if (arguments.Count > 0
            && !arguments.Any(argument =>
                argument.StartsWith("-", StringComparison.Ordinal)))
        {
            return null;
        }

        return BuildLaunchCommand(
            executable,
            arguments,
            startupDirectory,
            dayZRootPath,
            workingDirectory: null,
            limitations);
    }

    private static DayZLaunchCommand? TryParseStartProcess(
        string statement,
        IReadOnlyDictionary<string, StaticValue> variables,
        string startupDirectory,
        string dayZRootPath,
        ICollection<string> limitations)
    {
        var tokens = Tokenize(statement, limitations);

        if (tokens.Count == 0
            || !tokens[0].IsUnquotedValue("Start-Process"))
        {
            return null;
        }

        PowerShellToken? filePathToken = null;
        PowerShellToken? workingDirectoryToken = null;
        StaticValue? splattedFilePath = null;
        StaticValue? splattedArguments = null;
        StaticValue? splattedWorkingDirectory = null;
        var argumentTokens = new List<PowerShellToken>();
        var firstParameterIndex = 1;

        if (tokens.Count > 1
            && tokens[1].Kind != PowerShellTokenKind.Splat
            && !IsStartProcessParameter(tokens[1]))
        {
            filePathToken = tokens[1];
            firstParameterIndex = 2;
        }

        for (var index = firstParameterIndex; index < tokens.Count; index++)
        {
            var token = tokens[index];

            if (token.Kind == PowerShellTokenKind.Splat)
            {
                if (!variables.TryGetValue(token.Value, out var splat)
                    || splat.Members is null)
                {
                    limitations.Add(
                        $"{DescribeVariable(token.Value, isSplat: true)} is not a fully static Start-Process splat.");
                    continue;
                }

                splat.Members.TryGetValue("FilePath", out splattedFilePath);
                splat.Members.TryGetValue("ArgumentList", out splattedArguments);
                splat.Members.TryGetValue(
                    "WorkingDirectory",
                    out splattedWorkingDirectory);
                continue;
            }

            if (token.IsUnquotedValue("-FilePath"))
            {
                if (++index >= tokens.Count)
                {
                    limitations.Add(
                        "Start-Process has no static FilePath value.");
                    return null;
                }

                filePathToken = tokens[index];
                continue;
            }

            if (token.IsUnquotedValue("-WorkingDirectory"))
            {
                if (++index >= tokens.Count)
                {
                    limitations.Add(
                        "Start-Process has no static WorkingDirectory value.");
                    continue;
                }

                workingDirectoryToken = tokens[index];
                continue;
            }

            if (token.IsUnquotedValue("-ArgumentList"))
            {
                index++;

                while (index < tokens.Count)
                {
                    var argument = tokens[index];

                    if (IsStartProcessParameter(argument))
                        break;

                    argumentTokens.Add(argument);
                    index++;
                }

                index--;
            }
        }

        string? executable = splattedFilePath?.Scalar;

        if (filePathToken is not null
            && !TryResolveToken(
                filePathToken,
                variables,
                out executable,
                limitations,
                reportUnresolvedVariable: true))
        {
            executable = null;
        }

        if (executable is null
            || !IsDayZExecutable(executable))
        {
            limitations.Add(
                "Start-Process FilePath was not a statically resolved DayZ server executable.");

            return null;
        }

        var arguments = new List<string>();

        if (splattedArguments?.Items is not null)
            arguments.AddRange(splattedArguments.Items);
        else if (splattedArguments?.Scalar is not null)
            arguments.Add(splattedArguments.Scalar);

        foreach (var token in argumentTokens)
        {
            if (token.Kind == PowerShellTokenKind.Comma)
                continue;

            if (token.Kind == PowerShellTokenKind.Variable)
            {
                if (!variables.TryGetValue(token.Value, out var variable))
                {
                    limitations.Add(
                        $"{DescribeVariable(token.Value)} could not be resolved statically.");
                    continue;
                }

                if (variable.Items is not null)
                    arguments.AddRange(variable.Items);
                else if (variable.Scalar is not null)
                    arguments.Add(variable.Scalar);

                continue;
            }

            if (!TryResolveToken(
                    token,
                    variables,
                    out var argument,
                    limitations,
                    reportUnresolvedVariable: true))
            {
                continue;
            }

            arguments.Add(argument);
        }

        string? workingDirectory = splattedWorkingDirectory?.Scalar;

        if (workingDirectoryToken is not null
            && !TryResolveToken(
                workingDirectoryToken,
                variables,
                out workingDirectory,
                limitations,
                reportUnresolvedVariable: true))
        {
            workingDirectory = null;
            limitations.Add(
                "Start-Process WorkingDirectory could not be resolved statically.");
        }

        return BuildLaunchCommand(
            executable,
            arguments,
            startupDirectory,
            dayZRootPath,
            workingDirectory,
            limitations);
    }

    private static DayZLaunchCommand? BuildLaunchCommand(
        string executable,
        IReadOnlyList<string> argumentTokens,
        string startupDirectory,
        string dayZRootPath,
        string? workingDirectory,
        ICollection<string> limitations)
    {
        if (!TryResolvePath(executable, startupDirectory, out var executablePath))
        {
            limitations.Add(
                "The PowerShell launch executable contains an invalid path.");
            return null;
        }

        var arguments = ReadArguments(argumentTokens);
        var executableDirectory = Path.GetDirectoryName(executablePath);
        var configurationBasePath = executableDirectory ?? startupDirectory;

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            if (TryResolvePath(
                    workingDirectory,
                    startupDirectory,
                    out var resolvedWorkingDirectory))
            {
                configurationBasePath = resolvedWorkingDirectory;
            }
            else
            {
                limitations.Add(
                    "Start-Process WorkingDirectory contains an invalid path.");
            }
        }

        var configurationPath = ResolveOptionalPath(
            arguments.GetValueOrDefault("config"),
            configurationBasePath,
            "configuration",
            limitations);
        var clientMods = ResolveModPaths(
            arguments.GetValueOrDefault("mod"),
            dayZRootPath,
            "client mod",
            limitations);
        var serverMods = ResolveModPaths(
            arguments.GetValueOrDefault("serverMod"),
            dayZRootPath,
            "server-only mod",
            limitations);
        var profilesPath = ResolveOptionalPath(
            arguments.GetValueOrDefault("profiles"),
            dayZRootPath,
            "profiles",
            limitations);
        var storagePath = ResolveOptionalPath(
            arguments.GetValueOrDefault("storage"),
            dayZRootPath,
            "storage",
            limitations);
        var battleEyePath = ResolveOptionalPath(
            arguments.GetValueOrDefault("BEpath"),
            dayZRootPath,
            "BE path",
            limitations);

        return new DayZLaunchCommand(
            executablePath,
            configurationPath,
            clientMods,
            serverMods,
            profilesPath,
            storagePath,
            arguments.GetValueOrDefault("mission"),
            arguments.GetValueOrDefault("port"),
            battleEyePath);
    }

    private static string CreateCommandSignature(DayZLaunchCommand command) =>
        string.Join(
            '\u001f',
            command.ExecutablePath,
            command.ConfigurationPath ?? string.Empty,
            string.Join('\u001e', command.ClientModPaths),
            string.Join('\u001e', command.ServerModPaths),
            command.ProfilesPath ?? string.Empty,
            command.StoragePath ?? string.Empty,
            command.Mission ?? string.Empty,
            command.Port ?? string.Empty,
            command.BattleEyePath ?? string.Empty);

    private static Dictionary<string, string> ReadArguments(
        IReadOnlyList<string> tokens)
    {
        var values = new Dictionary<string, List<string>>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];

            foreach (var argumentName in SupportedArguments)
            {
                var prefix = $"-{argumentName}=";
                string? value = null;

                if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = token[prefix.Length..];
                }
                else if (string.Equals(
                             token,
                             $"-{argumentName}",
                             StringComparison.OrdinalIgnoreCase)
                         && index + 1 < tokens.Count)
                {
                    value = tokens[++index];
                }

                if (value is null)
                    continue;

                if (!values.TryGetValue(argumentName, out var entries))
                {
                    entries = [];
                    values[argumentName] = entries;
                }

                entries.Add(value);
                break;
            }
        }

        return values.ToDictionary(
            pair => pair.Key,
            pair => pair.Key.Equals("mod", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("serverMod", StringComparison.OrdinalIgnoreCase)
                ? string.Join(';', pair.Value)
                : pair.Value[^1],
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryResolveScalar(
        string expression,
        IReadOnlyDictionary<string, StaticValue> variables,
        out string value,
        ICollection<string> limitations)
    {
        var tokens = Tokenize(expression.Trim(), limitations);

        if (tokens.Count != 1)
        {
            value = string.Empty;
            limitations.Add(
                "A PowerShell argument collection contains a non-scalar expression.");
            return false;
        }

        return TryResolveToken(
            tokens[0],
            variables,
            out value,
            limitations,
            reportUnresolvedVariable: true);
    }

    private static bool TryResolveToken(
        PowerShellToken token,
        IReadOnlyDictionary<string, StaticValue> variables,
        out string value,
        ICollection<string> limitations,
        bool reportUnresolvedVariable = true)
    {
        value = string.Empty;

        if (token.Kind == PowerShellTokenKind.Variable)
        {
            if (variables.TryGetValue(token.Value, out var variable)
                && variable.Scalar is not null)
            {
                value = variable.Scalar;
                return true;
            }

            if (reportUnresolvedVariable)
            {
                limitations.Add(
                    $"{DescribeVariable(token.Value)} could not be resolved statically.");
            }

            return false;
        }

        if (token.Kind == PowerShellTokenKind.DoubleQuoted)
        {
            return TryInterpolate(
                token.Value,
                variables,
                out value,
                limitations);
        }

        if (token.Kind is PowerShellTokenKind.SingleQuoted
            or PowerShellTokenKind.Word)
        {
            if (token.Value.Contains("$(", StringComparison.Ordinal))
            {
                limitations.Add(
                    "PowerShell command substitution is not statically evaluated.");
                return false;
            }

            if (token.Kind == PowerShellTokenKind.Word
                && (token.Value.StartsWith('$')
                    || token.Value.StartsWith('[')
                    || token.Value.Contains("::", StringComparison.Ordinal)
                    || token.Value.Contains('(')
                    || token.Value.Contains(')')
                    || token.Value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || token.Value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                limitations.Add(
                    "A PowerShell runtime or remote expression was not statically evaluated.");
                return false;
            }

            value = token.Value;
            return true;
        }

        return false;
    }

    private static bool TryInterpolate(
        string template,
        IReadOnlyDictionary<string, StaticValue> variables,
        out string value,
        ICollection<string> limitations)
    {
        var unresolved = false;
        var interpolated = InterpolationPattern().Replace(
            template,
            match =>
            {
                var name = match.Groups["braced"].Success
                    ? match.Groups["braced"].Value
                    : match.Groups["plain"].Value;

                if (variables.TryGetValue(name, out var variable)
                    && variable.Scalar is not null)
                {
                    return variable.Scalar;
                }

                unresolved = true;
                return string.Empty;
            });

        if (unresolved)
        {
            limitations.Add(
                "A double-quoted PowerShell string contains an unresolved variable.");
            value = string.Empty;
            return false;
        }

        value = interpolated;
        return true;
    }

    private static IReadOnlyList<AnalyzedStatement> ExpandStatements(
        IEnumerable<string> statements,
        ICollection<string> limitations,
        ref int nextScopeId,
        bool isConditional = false,
        int scopeId = 0)
    {
        var expanded = new List<AnalyzedStatement>();

        foreach (var statement in statements)
        {
            var candidate = statement.Trim();

            if (candidate.Length == 0)
                continue;

            if (!IsControlContainer(candidate))
            {
                expanded.Add(
                    new AnalyzedStatement(
                        candidate,
                        isConditional,
                        scopeId));
                continue;
            }

            limitations.Add(
                "PowerShell runtime control flow was not evaluated; enclosed static syntax was analyzed conditionally.");

            foreach (var body in ExtractBraceBodies(candidate))
            {
                var bodyScopeId = ++nextScopeId;
                expanded.AddRange(
                    ExpandStatements(
                        SplitStatements(body, limitations),
                        limitations,
                        ref nextScopeId,
                        isConditional: true,
                        scopeId: bodyScopeId));
            }
        }

        return expanded;
    }

    private static bool IsControlContainer(string statement)
    {
        var command = Tokenize(statement, []).FirstOrDefault()?.Value
            ?? string.Empty;

        return command.Equals("if", StringComparison.OrdinalIgnoreCase)
            || command.Equals("elseif", StringComparison.OrdinalIgnoreCase)
            || command.Equals("else", StringComparison.OrdinalIgnoreCase)
            || command.Equals("switch", StringComparison.OrdinalIgnoreCase)
            || command.Equals("foreach", StringComparison.OrdinalIgnoreCase)
            || command.Equals("for", StringComparison.OrdinalIgnoreCase)
            || command.Equals("while", StringComparison.OrdinalIgnoreCase)
            || command.Equals("do", StringComparison.OrdinalIgnoreCase)
            || command.Equals("function", StringComparison.OrdinalIgnoreCase)
            || command.Equals("try", StringComparison.OrdinalIgnoreCase)
            || command.Equals("catch", StringComparison.OrdinalIgnoreCase)
            || command.Equals("finally", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ExtractBraceBodies(string statement)
    {
        var bodies = new List<string>();
        var quote = '\0';
        var escaped = false;
        var depth = 0;
        var bodyStart = -1;

        for (var index = 0; index < statement.Length; index++)
        {
            var character = statement[index];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '`' && quote != '\'')
            {
                escaped = true;
                continue;
            }

            if (quote == '\0' && character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character == quote)
            {
                quote = '\0';
                continue;
            }

            if (quote != '\0')
                continue;

            if (character == '{')
            {
                if (depth++ == 0)
                    bodyStart = index + 1;
            }
            else if (character == '}'
                     && depth > 0
                     && --depth == 0
                     && bodyStart >= 0)
            {
                bodies.Add(statement[bodyStart..index]);
                bodyStart = -1;
            }
        }

        return bodies;
    }

    private static bool IsUnsupportedBoundary(
        string statement,
        ICollection<string> limitations)
    {
        var normalized = statement.TrimStart();
        var firstToken = Tokenize(normalized, limitations).FirstOrDefault();
        var command = firstToken?.Value ?? string.Empty;
        var unsupportedCommand =
            command.Equals("Invoke-Expression", StringComparison.OrdinalIgnoreCase)
            || command.Equals("iex", StringComparison.OrdinalIgnoreCase)
            || command.Equals("Invoke-Command", StringComparison.OrdinalIgnoreCase)
            || command.Equals("powershell", StringComparison.OrdinalIgnoreCase)
            || command.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase)
            || command.Equals("pwsh", StringComparison.OrdinalIgnoreCase)
            || command.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase)
            || command.Equals("cmd", StringComparison.OrdinalIgnoreCase)
            || command.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase)
            || command.Equals("Import-Module", StringComparison.OrdinalIgnoreCase)
            || command.Equals("if", StringComparison.OrdinalIgnoreCase)
            || command.Equals("switch", StringComparison.OrdinalIgnoreCase)
            || command.Equals("foreach", StringComparison.OrdinalIgnoreCase)
            || command.Equals("for", StringComparison.OrdinalIgnoreCase)
            || command.Equals("while", StringComparison.OrdinalIgnoreCase)
            || command.Equals("do", StringComparison.OrdinalIgnoreCase)
            || command.Equals("function", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(". ", StringComparison.Ordinal);

        if (unsupportedCommand
            || normalized.Contains("$(", StringComparison.Ordinal)
            || normalized.Contains('{')
            || normalized.Contains('}')
            || normalized.StartsWith("& (", StringComparison.Ordinal)
            || ContainsUnquotedPipeline(normalized))
        {
            limitations.Add(
                "A PowerShell runtime construct was ignored by static startup analysis.");
            return true;
        }

        return false;
    }

    private static bool ContainsUnquotedPipeline(string value)
    {
        var quote = '\0';
        var escaped = false;

        foreach (var character in value)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '`' && quote != '\'')
            {
                escaped = true;
                continue;
            }

            if (quote == '\0' && character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character == quote)
            {
                quote = '\0';
                continue;
            }

            if (quote == '\0' && character == '|')
                return true;
        }

        return false;
    }

    private static string RemoveCommentsAndContinuations(
        string content,
        ICollection<string> limitations)
    {
        var result = new StringBuilder(content.Length);
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var index = 0; index < content.Length; index++)
        {
            var current = content[index];
            var next = index + 1 < content.Length
                ? content[index + 1]
                : '\0';

            if (inLineComment)
            {
                if (current is '\r' or '\n')
                {
                    inLineComment = false;
                    result.Append(current);
                }

                continue;
            }

            if (inBlockComment)
            {
                if (current == '#' && next == '>')
                {
                    inBlockComment = false;
                    index++;
                    result.Append(' ');
                }

                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && current == '<' && next == '#')
            {
                inBlockComment = true;
                index++;
                result.Append(' ');
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && current == '#')
            {
                inLineComment = true;
                continue;
            }

            if (!inSingleQuote && current == '`')
            {
                if (next == '\r'
                    && index + 2 < content.Length
                    && content[index + 2] == '\n')
                {
                    result.Append(' ');
                    index += 2;
                    continue;
                }

                if (next == '\n')
                {
                    result.Append(' ');
                    index++;
                    continue;
                }

                result.Append(current);

                if (next != '\0')
                    result.Append(content[++index]);

                continue;
            }

            if (!inDoubleQuote && current == '\'')
            {
                if (inSingleQuote && next == '\'')
                {
                    result.Append("''");
                    index++;
                    continue;
                }

                inSingleQuote = !inSingleQuote;
            }
            else if (!inSingleQuote && current == '"')
            {
                inDoubleQuote = !inDoubleQuote;
            }

            result.Append(current);
        }

        if (inBlockComment)
            limitations.Add("The PowerShell script contains an unterminated block comment.");

        if (inSingleQuote || inDoubleQuote)
            limitations.Add("The PowerShell script contains an unterminated string.");

        return result.ToString();
    }

    private static IReadOnlyList<string> SplitStatements(
        string content,
        ICollection<string> limitations)
    {
        var statements = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var escaped = false;
        var parentheses = 0;
        var braces = 0;

        foreach (var character in content)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (character == '`' && quote != '\'')
            {
                current.Append(character);
                escaped = true;
                continue;
            }

            if (quote == '\0' && character is '\'' or '"')
                quote = character;
            else if (character == quote)
                quote = '\0';

            if (quote == '\0')
            {
                if (character == '(')
                    parentheses++;
                else if (character == ')')
                    parentheses--;
                else if (character == '{')
                    braces++;
                else if (character == '}')
                    braces--;

                if ((character is '\r' or '\n' or ';')
                    && parentheses == 0
                    && braces == 0)
                {
                    if (current.Length > 0)
                    {
                        statements.Add(current.ToString());
                        current.Clear();
                    }

                    continue;
                }
            }

            current.Append(character);
        }

        if (current.Length > 0)
            statements.Add(current.ToString());

        if (parentheses != 0 || braces != 0)
            limitations.Add("PowerShell grouping delimiters are not balanced.");

        return statements;
    }

    private static IReadOnlyList<string> SplitCollection(string content)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var escaped = false;

        foreach (var character in content)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (character == '`' && quote != '\'')
            {
                current.Append(character);
                escaped = true;
                continue;
            }

            if (quote == '\0' && character is '\'' or '"')
                quote = character;
            else if (character == quote)
                quote = '\0';

            if (quote == '\0' && character is ',' or '\r' or '\n')
            {
                if (!string.IsNullOrWhiteSpace(current.ToString()))
                    result.Add(current.ToString().Trim());

                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (!string.IsNullOrWhiteSpace(current.ToString()))
            result.Add(current.ToString().Trim());

        return result;
    }

    private static IReadOnlyList<string> SplitHashtableEntries(string content)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var escaped = false;
        var parentheses = 0;

        foreach (var character in content)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (character == '`' && quote != '\'')
            {
                current.Append(character);
                escaped = true;
                continue;
            }

            if (quote == '\0' && character is '\'' or '"')
                quote = character;
            else if (character == quote)
                quote = '\0';

            if (quote == '\0')
            {
                if (character == '(')
                    parentheses++;
                else if (character == ')')
                    parentheses--;

                if ((character is ';' or '\r' or '\n')
                    && parentheses == 0)
                {
                    if (!string.IsNullOrWhiteSpace(current.ToString()))
                        result.Add(current.ToString().Trim());

                    current.Clear();
                    continue;
                }
            }

            current.Append(character);
        }

        if (!string.IsNullOrWhiteSpace(current.ToString()))
            result.Add(current.ToString().Trim());

        return result;
    }

    private static IReadOnlyList<PowerShellToken> Tokenize(
        string value,
        ICollection<string> limitations)
    {
        var tokens = new List<PowerShellToken>();

        for (var index = 0; index < value.Length;)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                index++;
                continue;
            }

            if (value[index] == ',')
            {
                tokens.Add(new PowerShellToken(PowerShellTokenKind.Comma, ","));
                index++;
                continue;
            }

            if (value[index] is '\'' or '"')
            {
                var quote = value[index++];
                var text = new StringBuilder();
                var closed = false;

                while (index < value.Length)
                {
                    var current = value[index++];

                    if (quote == '\''
                        && current == '\''
                        && index < value.Length
                        && value[index] == '\'')
                    {
                        text.Append('\'');
                        index++;
                        continue;
                    }

                    if (quote == '"'
                        && current == '`'
                        && index < value.Length)
                    {
                        text.Append(value[index++]);
                        continue;
                    }

                    if (current == quote)
                    {
                        closed = true;
                        break;
                    }

                    text.Append(current);
                }

                if (!closed)
                    limitations.Add("A PowerShell token contains an unterminated string.");

                tokens.Add(
                    new PowerShellToken(
                        quote == '\''
                            ? PowerShellTokenKind.SingleQuoted
                            : PowerShellTokenKind.DoubleQuoted,
                        text.ToString()));
                continue;
            }

            var wordBuilder = new StringBuilder();

            while (index < value.Length)
            {
                if (value[index] == '`'
                    && index + 1 < value.Length)
                {
                    wordBuilder.Append(value[index + 1]);
                    index += 2;
                    continue;
                }

                if (char.IsWhiteSpace(value[index])
                    || value[index] == ',')
                {
                    break;
                }

                wordBuilder.Append(value[index]);
                index++;
            }

            var word = wordBuilder.ToString();
            var kind = word.StartsWith('@')
                && word.Length > 1
                && IsVariableName(word[1..])
                    ? PowerShellTokenKind.Splat
                    : word.StartsWith('$')
                      && word.Length > 1
                      && IsVariableName(word[1..])
                        ? PowerShellTokenKind.Variable
                        : PowerShellTokenKind.Word;

            tokens.Add(
                new PowerShellToken(
                    kind,
                    kind is PowerShellTokenKind.Variable or PowerShellTokenKind.Splat
                        ? word[1..]
                        : word));
        }

        return tokens;
    }

    private static bool IsStartProcessParameter(PowerShellToken token)
    {
        return token.Kind == PowerShellTokenKind.Word
            && token.Value.StartsWith('-')
            && !SupportedArguments.Any(argument =>
                token.Value.StartsWith(
                    $"-{argument}",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsVariableName(string value) =>
        value.Length > 0
        && (char.IsLetter(value[0]) || value[0] == '_')
        && value.Skip(1).All(character =>
            char.IsLetterOrDigit(character) || character == '_');

    private static string DescribeVariable(
        string name,
        bool isSplat = false)
    {
        if (IsSensitiveName(name))
        {
            return "A sensitive PowerShell variable";
        }

        return isSplat
            ? $"PowerShell argument splat '@{name}'"
            : $"PowerShell variable '${name}'";
    }

    private static bool IsSensitiveName(string name) =>
        name.Contains("password", StringComparison.OrdinalIgnoreCase)
        || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || name.Contains("credential", StringComparison.OrdinalIgnoreCase)
        || name.Contains("token", StringComparison.OrdinalIgnoreCase)
        || name.Contains("webhook", StringComparison.OrdinalIgnoreCase);

    private static bool IsDayZExecutable(string value)
    {
        try
        {
            return string.Equals(
                Path.GetFileName(value.Trim().Trim('"')),
                "DayZServer_x64.exe",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ResolveModPaths(
        string? value,
        string basePath,
        string argumentDescription,
        ICollection<string> limitations)
    {
        if (TryResolveModPaths(value, basePath, out var paths))
            return paths;

        limitations.Add(
            $"A statically recovered {argumentDescription} argument contains an invalid path.");
        return [];
    }

    private static string? ResolveOptionalPath(
        string? value,
        string basePath,
        string argumentDescription,
        ICollection<string> limitations)
    {
        if (TryResolveOptionalPath(value, basePath, out var path))
            return path;

        limitations.Add(
            $"The statically recovered {argumentDescription} argument contains an invalid path.");
        return null;
    }

    private static bool TryResolveModPaths(
        string? value,
        string basePath,
        out IReadOnlyList<string> paths)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            paths = [];
            return true;
        }

        var result = new List<string>();

        foreach (var item in value.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            if (!TryResolvePath(item, basePath, out var resolved))
            {
                paths = [];
                return false;
            }

            result.Add(resolved);
        }

        paths = result;
        return true;
    }

    private static bool TryResolveOptionalPath(
        string? value,
        string basePath,
        out string? path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            path = null;
            return true;
        }

        var success = TryResolvePath(value, basePath, out var resolved);
        path = success ? resolved : null;
        return success;
    }

    private static bool TryResolvePath(
        string value,
        string basePath,
        out string path)
    {
        path = string.Empty;

        if (string.IsNullOrWhiteSpace(value)
            || value.IndexOf('\0') >= 0)
        {
            return false;
        }

        try
        {
            var normalized = value.Trim().Trim('"');
            path = Path.GetFullPath(
                Path.IsPathFullyQualified(normalized)
                    ? normalized
                    : Path.Combine(basePath, normalized));
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            return false;
        }
    }

    [GeneratedRegex(
        @"^\$(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?<operation>\+=|=)\s*(?<expression>[\s\S]+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex AssignmentPattern();

    [GeneratedRegex(
        @"(?is)\bparam\s*\((?<body>.*?)\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ParameterBlockPattern();

    [GeneratedRegex(
        @"^\s*(?:\[[^\]]+\]\s*)?\$(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<expression>[\s\S]+?)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ParameterDefaultPattern();

    [GeneratedRegex(
        @"\$(?:\{(?<braced>[A-Za-z_][A-Za-z0-9_]*)\}|(?<plain>[A-Za-z_][A-Za-z0-9_]*))",
        RegexOptions.CultureInvariant)]
    private static partial Regex InterpolationPattern();

    private sealed record AnalyzedStatement(
        string Text,
        bool IsConditional,
        int ScopeId);

    private enum PowerShellTokenKind
    {
        Word,
        SingleQuoted,
        DoubleQuoted,
        Variable,
        Splat,
        Comma
    }

    private sealed record PowerShellToken(
        PowerShellTokenKind Kind,
        string Value)
    {
        public bool IsUnquotedValue(string value) =>
            Kind == PowerShellTokenKind.Word
            && Value.Equals(value, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record StaticValue(
        string? Scalar,
        IReadOnlyList<string>? Items,
        IReadOnlyDictionary<string, StaticValue>? Members)
    {
        public static StaticValue Empty { get; } = new(null, null, null);

        public static StaticValue FromScalar(string value) =>
            new(value, null, null);

        public static StaticValue FromArray(IReadOnlyList<string> items) =>
            new(null, items.ToArray(), null);

        public static StaticValue FromMembers(
            IReadOnlyDictionary<string, StaticValue> members) =>
            new(
                null,
                null,
                new Dictionary<string, StaticValue>(
                    members,
                    StringComparer.OrdinalIgnoreCase));

        public bool TryAppend(
            StaticValue appended,
            out StaticValue combined)
        {
            if (Items is not null)
            {
                var items = Items.ToList();

                if (appended.Items is not null)
                    items.AddRange(appended.Items);
                else if (appended.Scalar is not null)
                    items.Add(appended.Scalar);
                else
                {
                    combined = Empty;
                    return false;
                }

                combined = FromArray(items);
                return true;
            }

            if (Scalar is not null
                && appended.Scalar is not null)
            {
                combined = FromScalar(Scalar + appended.Scalar);
                return true;
            }

            combined = Empty;
            return false;
        }
    }
}
