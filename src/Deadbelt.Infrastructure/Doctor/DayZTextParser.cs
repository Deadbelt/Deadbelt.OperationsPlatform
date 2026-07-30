using System.Text;

namespace Deadbelt.Infrastructure.Doctor;

internal sealed record DayZAssignment(
    string Name,
    string Value,
    IReadOnlyList<string> Scope);

internal sealed record DayZTextParseResult(
    IReadOnlyList<DayZAssignment> Assignments,
    IReadOnlyList<string> Limitations);

internal static class DayZTextParser
{
    public static DayZTextParseResult Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var limitations = new List<string>();
        var tokens = Tokenize(content, limitations);
        var assignments = new List<DayZAssignment>();
        var position = 0;

        ParseBlock(
            tokens,
            ref position,
            [],
            expectClosingBrace: false,
            assignments,
            limitations);

        return new DayZTextParseResult(
            assignments,
            limitations.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void ParseBlock(
        IReadOnlyList<Token> tokens,
        ref int position,
        IReadOnlyList<string> scope,
        bool expectClosingBrace,
        ICollection<DayZAssignment> assignments,
        ICollection<string> limitations)
    {
        while (position < tokens.Count)
        {
            if (tokens[position].IsSymbol("}"))
            {
                position++;

                if (!expectClosingBrace)
                    limitations.Add("Configuration contains an unexpected closing brace.");

                return;
            }

            if (tokens[position].IsIdentifier("class"))
            {
                ParseClass(
                    tokens,
                    ref position,
                    scope,
                    assignments,
                    limitations);
                continue;
            }

            if (tokens[position].Kind == TokenKind.Identifier
                && position + 1 < tokens.Count
                && tokens[position + 1].IsSymbol("="))
            {
                ParseAssignment(
                    tokens,
                    ref position,
                    scope,
                    assignments,
                    limitations);
                continue;
            }

            if (tokens[position].IsSymbol("{"))
            {
                limitations.Add("Configuration contains an unsupported anonymous block.");
                position++;
                ParseBlock(
                    tokens,
                    ref position,
                    scope,
                    expectClosingBrace: true,
                    assignments,
                    limitations);
                continue;
            }

            if (tokens[position].IsSymbol("="))
                limitations.Add("Configuration contains an incomplete assignment.");

            position++;
        }

        if (expectClosingBrace)
            limitations.Add("Configuration braces are not balanced.");
    }

    private static void ParseClass(
        IReadOnlyList<Token> tokens,
        ref int position,
        IReadOnlyList<string> scope,
        ICollection<DayZAssignment> assignments,
        ICollection<string> limitations)
    {
        position++;

        if (position >= tokens.Count
            || tokens[position].Kind != TokenKind.Identifier)
        {
            limitations.Add("A class declaration does not have a supported name.");
            SkipToStatementBoundary(tokens, ref position);
            return;
        }

        var className = tokens[position++].Value;

        while (position < tokens.Count
               && !tokens[position].IsSymbol("{")
               && !tokens[position].IsSymbol(";")
               && !tokens[position].IsSymbol("}"))
        {
            position++;
        }

        if (position >= tokens.Count || !tokens[position].IsSymbol("{"))
        {
            limitations.Add($"Class '{className}' does not have a complete body.");
            return;
        }

        position++;
        ParseBlock(
            tokens,
            ref position,
            scope.Concat([className]).ToArray(),
            expectClosingBrace: true,
            assignments,
            limitations);

        if (position < tokens.Count && tokens[position].IsSymbol(";"))
            position++;
    }

    private static void ParseAssignment(
        IReadOnlyList<Token> tokens,
        ref int position,
        IReadOnlyList<string> scope,
        ICollection<DayZAssignment> assignments,
        ICollection<string> limitations)
    {
        var name = tokens[position].Value;
        position += 2;

        if (position >= tokens.Count
            || tokens[position].Kind is not (
                TokenKind.Identifier
                or TokenKind.String
                or TokenKind.Scalar))
        {
            limitations.Add($"Assignment '{name}' does not have a supported scalar value.");
            SkipToStatementBoundary(tokens, ref position);
            return;
        }

        var value = tokens[position++].Value;

        if (position >= tokens.Count || !tokens[position].IsSymbol(";"))
        {
            limitations.Add($"Assignment '{name}' is malformed or uses unsupported syntax.");
            SkipToStatementBoundary(tokens, ref position);
            return;
        }

        position++;
        assignments.Add(
            new DayZAssignment(
                name,
                value,
                scope.ToArray()));
    }

    private static void SkipToStatementBoundary(
        IReadOnlyList<Token> tokens,
        ref int position)
    {
        while (position < tokens.Count
               && !tokens[position].IsSymbol(";")
               && !tokens[position].IsSymbol("}"))
        {
            position++;
        }

        if (position < tokens.Count && tokens[position].IsSymbol(";"))
            position++;
    }

    private static IReadOnlyList<Token> Tokenize(
        string content,
        ICollection<string> limitations)
    {
        var tokens = new List<Token>();

        for (var index = 0; index < content.Length;)
        {
            var current = content[index];
            var next = index + 1 < content.Length
                ? content[index + 1]
                : '\0';

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '/' && next == '/')
            {
                index += 2;

                while (index < content.Length
                       && content[index] is not '\r' and not '\n')
                {
                    index++;
                }

                continue;
            }

            if (current == '/' && next == '*')
            {
                index += 2;
                var closed = false;

                while (index + 1 < content.Length)
                {
                    if (content[index] == '*' && content[index + 1] == '/')
                    {
                        index += 2;
                        closed = true;
                        break;
                    }

                    index++;
                }

                if (!closed)
                {
                    limitations.Add("Configuration contains an unterminated block comment.");
                    index = content.Length;
                }

                continue;
            }

            if (current == '"')
            {
                var value = new StringBuilder();
                var closed = false;
                index++;

                while (index < content.Length)
                {
                    current = content[index++];

                    if (current == '\\' && index < content.Length)
                    {
                        value.Append(content[index++]);
                        continue;
                    }

                    if (current == '"')
                    {
                        closed = true;
                        break;
                    }

                    value.Append(current);
                }

                if (!closed)
                    limitations.Add("Configuration contains an unterminated quoted string.");

                tokens.Add(new Token(TokenKind.String, value.ToString()));
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                var start = index++;

                while (index < content.Length
                       && (char.IsLetterOrDigit(content[index])
                           || content[index] == '_'))
                {
                    index++;
                }

                tokens.Add(
                    new Token(
                        TokenKind.Identifier,
                        content[start..index]));
                continue;
            }

            if (current is '{' or '}' or '=' or ';' or ':' or ',')
            {
                tokens.Add(new Token(TokenKind.Symbol, current.ToString()));
                index++;
                continue;
            }

            var scalarStart = index++;

            while (index < content.Length
                   && !char.IsWhiteSpace(content[index])
                   && content[index] is not '{' and not '}' and not '=' and not ';'
                   && !(content[index] == '/'
                        && index + 1 < content.Length
                        && content[index + 1] is '/' or '*'))
            {
                index++;
            }

            tokens.Add(
                new Token(
                    TokenKind.Scalar,
                    content[scalarStart..index]));
        }

        return tokens;
    }

    private enum TokenKind
    {
        Identifier,
        String,
        Scalar,
        Symbol
    }

    private sealed record Token(
        TokenKind Kind,
        string Value)
    {
        public bool IsSymbol(string value) =>
            Kind == TokenKind.Symbol
            && string.Equals(Value, value, StringComparison.Ordinal);

        public bool IsIdentifier(string value) =>
            Kind == TokenKind.Identifier
            && string.Equals(Value, value, StringComparison.OrdinalIgnoreCase);
    }
}
