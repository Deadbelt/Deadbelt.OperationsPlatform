namespace Deadbelt.Infrastructure.Doctor;

internal sealed class PowerShellStartupParseResult
{
    public PowerShellStartupParseResult(
        IEnumerable<DayZLaunchCommand> commands,
        IEnumerable<string> limitations)
    {
        Commands = commands.ToArray();
        Limitations = limitations
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<DayZLaunchCommand> Commands { get; }

    public IReadOnlyList<string> Limitations { get; }

    public bool IsPartial => Limitations.Count > 0;
}
