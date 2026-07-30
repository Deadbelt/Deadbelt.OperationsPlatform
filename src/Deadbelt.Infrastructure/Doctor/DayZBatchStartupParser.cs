using System.Text;
using System.Text.RegularExpressions;

namespace Deadbelt.Infrastructure.Doctor;

internal static partial class DayZBatchStartupParser
{
    private static readonly string[] SupportedArguments =
    [
        "config",
        "mod",
        "serverMod",
        "profiles",
        "storage",
        "mission",
        "port",
        "BEpath"
    ];

    public static BatchStartupParseResult Parse(
        string content,
        string startupFilePath,
        string dayZRootPath)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(startupFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dayZRootPath);

        var variables = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var commands = new List<DayZLaunchCommand>();
        var limitations = new List<string>();
        var startupDirectory = Path.GetDirectoryName(startupFilePath)
            ?? dayZRootPath;

        foreach (var logicalLine in JoinContinuations(content, limitations))
        {
            var line = logicalLine.Trim();
            var commandLine = line.StartsWith('@')
                ? line[1..].TrimStart()
                : line;

            if (line.Length == 0
                || IsCommand(commandLine, "rem")
                || line.StartsWith("::", StringComparison.Ordinal)
                || line.StartsWith(':')
                || IsCommand(commandLine, "echo"))
            {
                continue;
            }

            if (TryParseSet(line, variables))
                continue;

            if (ContainsUnsupportedControlFlow(commandLine)
                || ContainsCommandOperator(commandLine))
            {
                limitations.Add(
                    $"Unsupported batch control flow was ignored: {Summarize(line)}");
                continue;
            }

            var expandedLine = VariablePattern().Replace(
                commandLine,
                match =>
                {
                    var name = match.Groups["name"].Value;

                    if (variables.TryGetValue(name, out var value))
                        return value;

                    limitations.Add(
                        $"Environment variable %{name}% could not be resolved.");
                    return match.Value;
                });

            var hasDelayedExpansion = expandedLine.Contains('!');

            if (hasDelayedExpansion)
            {
                limitations.Add(
                    "Delayed batch-variable expansion is not supported.");
            }

            var tokens = Tokenize(expandedLine, limitations);
            if (tokens.Count == 0
                || !string.Equals(
                    Path.GetFileName(tokens[0]),
                    "DayZServer_x64.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (hasDelayedExpansion
                || VariablePattern().IsMatch(tokens[0]))
            {
                limitations.Add(
                    "A launch command with unresolved variable expansion was ignored.");
                continue;
            }

            var arguments = ReadArguments(tokens.Skip(1).ToArray());

            if (!TryResolvePath(tokens[0], startupDirectory, out var executablePath)
                || !TryResolveOptionalPath(
                    arguments.GetValueOrDefault("config"),
                    startupDirectory,
                    out var configurationPath)
                || !TryResolveModPaths(
                    arguments.GetValueOrDefault("mod"),
                    dayZRootPath,
                    out var clientMods)
                || !TryResolveModPaths(
                    arguments.GetValueOrDefault("serverMod"),
                    dayZRootPath,
                    out var serverMods)
                || !TryResolveOptionalPath(
                    arguments.GetValueOrDefault("profiles"),
                    dayZRootPath,
                    out var profilesPath)
                || !TryResolveOptionalPath(
                    arguments.GetValueOrDefault("storage"),
                    dayZRootPath,
                    out var storagePath)
                || !TryResolveOptionalPath(
                    arguments.GetValueOrDefault("BEpath"),
                    dayZRootPath,
                    out var battleEyePath))
            {
                limitations.Add("A launch command contains an invalid path.");
                continue;
            }

            commands.Add(
                new DayZLaunchCommand(
                    executablePath,
                    configurationPath,
                    clientMods,
                    serverMods,
                    profilesPath,
                    storagePath,
                    arguments.GetValueOrDefault("mission"),
                    arguments.GetValueOrDefault("port"),
                    battleEyePath));
        }

        if (commands.Count > 1)
        {
            limitations.Add(
                "More than one DayZ server launch command was found.");
        }

        return new BatchStartupParseResult(
            commands,
            limitations);
    }

    private static IReadOnlyList<string> JoinContinuations(
        string content,
        ICollection<string> limitations)
    {
        var result = new List<string>();
        var current = new StringBuilder();

        using var reader = new StringReader(content);

        while (reader.ReadLine() is { } physicalLine)
        {
            var trimmedEnd = physicalLine.TrimEnd();
            var continued = trimmedEnd.EndsWith(
                "^",
                StringComparison.Ordinal);
            var segment = continued
                ? trimmedEnd[..^1]
                : physicalLine;

            current.Append(segment);

            if (continued)
            {
                current.Append(' ');
                continue;
            }

            result.Add(current.ToString());
            current.Clear();
        }

        if (current.Length > 0)
        {
            limitations.Add(
                "The final batch line has an incomplete continuation.");
            result.Add(current.ToString());
        }

        return result;
    }

    private static bool TryParseSet(
        string line,
        IDictionary<string, string> variables)
    {
        var normalized = line.StartsWith('@')
            ? line[1..].TrimStart()
            : line;

        if (!normalized.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
            return false;

        var assignment = normalized[4..].Trim();

        if (assignment.Length >= 2
            && assignment[0] == '"'
            && assignment[^1] == '"')
        {
            assignment = assignment[1..^1];
        }

        var equalsIndex = assignment.IndexOf('=');

        if (equalsIndex <= 0)
            return true;

        var name = assignment[..equalsIndex].Trim();
        var value = assignment[(equalsIndex + 1)..];

        if (name.Length > 0)
            variables[name] = value;

        return true;
    }

    private static bool ContainsUnsupportedControlFlow(string line)
    {
        var normalized = line.TrimStart('@').TrimStart();

        return normalized.StartsWith("if ", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("for ", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("goto ", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("call ", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("start ", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("powershell", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("pwsh", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("cmd ", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("cmd.exe ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsCommandOperator(string line)
    {
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (!quoted && line[index] is '&' or '|' or '>' or '<')
                return true;
        }

        return false;
    }

    private static bool IsCommand(
        string line,
        string command)
    {
        return string.Equals(line, command, StringComparison.OrdinalIgnoreCase)
            || line.StartsWith(
                $"{command} ",
                StringComparison.OrdinalIgnoreCase)
            || line.StartsWith(
                $"{command}\t",
                StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> Tokenize(
        string line,
        ICollection<string> limitations)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        foreach (var character in line)
        {
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        if (quoted)
            limitations.Add("An unterminated quoted argument was encountered.");

        return tokens;
    }

    private static Dictionary<string, string> ReadArguments(
        IReadOnlyList<string> tokens)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];

            foreach (var argumentName in SupportedArguments)
            {
                var prefix = $"-{argumentName}=";

                if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    result[argumentName] = token[prefix.Length..];
                    break;
                }

                if (string.Equals(
                        token,
                        $"-{argumentName}",
                        StringComparison.OrdinalIgnoreCase)
                    && index + 1 < tokens.Count)
                {
                    result[argumentName] = tokens[++index];
                    break;
                }
            }
        }

        return result;
    }

    private static bool TryResolveModPaths(
        string? value,
        string dayZRootPath,
        out IReadOnlyList<string> paths)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            paths = [];
            return true;
        }

        if (VariablePattern().IsMatch(value))
        {
            paths = [];
            return true;
        }

        var result = new List<string>();

        foreach (var path in value.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            if (!TryResolvePath(path, dayZRootPath, out var resolved))
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
        if (string.IsNullOrWhiteSpace(value)
            || VariablePattern().IsMatch(value))
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

    private static string Summarize(string value)
    {
        const int maximumLength = 80;

        return value.Length <= maximumLength
            ? value
            : $"{value[..maximumLength]}...";
    }

    [GeneratedRegex("%(?<name>[^%]+)%", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();
}
