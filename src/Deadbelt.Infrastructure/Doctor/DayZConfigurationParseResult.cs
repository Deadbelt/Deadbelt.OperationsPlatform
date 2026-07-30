namespace Deadbelt.Infrastructure.Doctor;

internal enum PasswordAdminState
{
    Missing = 0,
    Empty = 1,
    Present = 2
}

internal sealed class DayZConfigurationParseResult
{
    public DayZConfigurationParseResult(
        IReadOnlyDictionary<string, string> values,
        string? missionTemplate,
        PasswordAdminState passwordAdminState,
        IEnumerable<string> limitations)
    {
        Values = new Dictionary<string, string>(
            values,
            StringComparer.OrdinalIgnoreCase);
        MissionTemplate = missionTemplate;
        PasswordAdminState = passwordAdminState;
        Limitations = limitations
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyDictionary<string, string> Values { get; }

    public string? MissionTemplate { get; }

    public PasswordAdminState PasswordAdminState { get; }

    public IReadOnlyList<string> Limitations { get; }

    public bool IsPartial => Limitations.Count > 0;
}
