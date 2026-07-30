namespace Deadbelt.Infrastructure.Doctor;

internal sealed record DayZModMetadataParseResult(
    string? PublishedId,
    string? DisplayName,
    IReadOnlyList<string> Limitations);

internal static class DayZModMetadataParser
{
    private static readonly HashSet<string> DisplayNameKeys = new(
        ["name", "displayName"],
        StringComparer.OrdinalIgnoreCase);

    public static DayZModMetadataParseResult Parse(string content)
    {
        var parsed = DayZTextParser.Parse(content);
        var limitations = parsed.Limitations.ToList();
        var topLevel = parsed.Assignments
            .Where(assignment => assignment.Scope.Count == 0)
            .ToArray();
        var publishedAssignments = topLevel
            .Where(assignment => string.Equals(
                assignment.Name,
                "publishedid",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var displayAssignments = topLevel
            .Where(assignment => DisplayNameKeys.Contains(assignment.Name))
            .ToArray();

        if (publishedAssignments.Length > 1)
            limitations.Add("Assignment 'publishedid' is declared more than once.");

        if (displayAssignments.Length > 1)
            limitations.Add("More than one supported display-name assignment was found.");

        string? publishedId = null;

        if (publishedAssignments.Length > 0)
        {
            var candidate = publishedAssignments[^1].Value;

            if (candidate.All(char.IsDigit) && candidate.Length > 0)
                publishedId = candidate;
            else
                limitations.Add("The publishedid assignment is malformed.");
        }

        var displayName = displayAssignments
            .Select(assignment => assignment.Value)
            .LastOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return new DayZModMetadataParseResult(
            publishedId,
            displayName,
            limitations.Distinct(StringComparer.Ordinal).ToArray());
    }
}
