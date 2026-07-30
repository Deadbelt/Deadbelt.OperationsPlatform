namespace Deadbelt.Infrastructure.Doctor;

internal static class DayZConfigurationParser
{
    private static readonly HashSet<string> SafeKeys = new(
        [
            "hostname",
            "maxPlayers",
            "verifySignatures",
            "enableCfgGameplayFile",
            "forceSameBuild",
            "disableVoN",
            "vonCodecQuality",
            "serverTime",
            "serverTimeAcceleration",
            "serverNightTimeAcceleration",
            "instanceId",
            "storageAutoFix",
            "respawnTime",
            "loginQueueConcurrentPlayers",
            "loginQueueMaxPlayers"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static DayZConfigurationParseResult Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var parsed = DayZTextParser.Parse(content);
        var limitations = parsed.Limitations.ToList();
        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var topLevel = parsed.Assignments
            .Where(assignment => assignment.Scope.Count == 0)
            .ToArray();

        foreach (var group in topLevel.GroupBy(
                     assignment => assignment.Name,
                     StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
                limitations.Add($"Assignment '{group.Key}' is declared more than once.");

            var assignment = group.Last();

            if (SafeKeys.Contains(assignment.Name))
                values[assignment.Name] = assignment.Value;
        }

        var missionAssignments = parsed.Assignments
            .Where(assignment =>
                assignment.Scope.Count >= 1
                && string.Equals(
                    assignment.Scope[0],
                    "Missions",
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    assignment.Name,
                    "template",
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (missionAssignments.Length > 1)
            limitations.Add("More than one mission template assignment was found.");

        var passwordAssignments = topLevel
            .Where(assignment => string.Equals(
                assignment.Name,
                "passwordAdmin",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (passwordAssignments.Length > 1)
            limitations.Add("Assignment 'passwordAdmin' is declared more than once.");

        var passwordState = passwordAssignments.Length == 0
            ? PasswordAdminState.Missing
            : string.IsNullOrEmpty(passwordAssignments[^1].Value)
                ? PasswordAdminState.Empty
                : PasswordAdminState.Present;

        return new DayZConfigurationParseResult(
            values,
            missionAssignments.FirstOrDefault()?.Value,
            passwordState,
            limitations);
    }
}
