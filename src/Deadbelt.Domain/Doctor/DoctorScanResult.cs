namespace Deadbelt.Domain.Doctor;

public sealed class DoctorScanResult
{
    private DoctorScanResult(
        DoctorScanStatus status,
        DoctorInventory? inventory,
        IReadOnlyList<DoctorFinding> findings,
        DateTime startedUtc,
        DateTime completedUtc)
    {
        Status = status;
        Inventory = inventory;
        Findings = findings;
        StartedUtc = startedUtc;
        CompletedUtc = completedUtc;
    }

    public DoctorScanStatus Status { get; }

    public DoctorInventory? Inventory { get; }

    public IReadOnlyList<DoctorFinding> Findings { get; }

    public DateTime StartedUtc { get; }

    public DateTime CompletedUtc { get; }

    public TimeSpan Duration => CompletedUtc - StartedUtc;

    public int InformationCount => Findings.Count(
        finding => finding.Severity == DoctorSeverity.Information);

    public int WarningCount => Findings.Count(
        finding => finding.Severity == DoctorSeverity.Warning);

    public int ErrorCount => Findings.Count(
        finding => finding.Severity == DoctorSeverity.Error);

    public static DoctorScanResult Completed(
        DoctorInventory inventory,
        IEnumerable<DoctorFinding>? findings,
        TimeSpan duration)
    {
        ValidateDuration(duration);
        return Completed(
            inventory,
            findings,
            DateTime.UnixEpoch,
            DateTime.UnixEpoch + duration);
    }

    public static DoctorScanResult Completed(
        DoctorInventory inventory,
        IEnumerable<DoctorFinding>? findings,
        DateTime startedUtc,
        DateTime completedUtc)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ValidateTimestamps(startedUtc, completedUtc);

        return new DoctorScanResult(
            DoctorScanStatus.Completed,
            inventory,
            Snapshot(findings),
            startedUtc,
            completedUtc);
    }

    public static DoctorScanResult Cancelled(TimeSpan duration)
    {
        ValidateDuration(duration);
        return Cancelled(
            DateTime.UnixEpoch,
            DateTime.UnixEpoch + duration);
    }

    public static DoctorScanResult Cancelled(
        DateTime startedUtc,
        DateTime completedUtc)
    {
        ValidateTimestamps(startedUtc, completedUtc);

        return new DoctorScanResult(
            DoctorScanStatus.Cancelled,
            null,
            Array.AsReadOnly(Array.Empty<DoctorFinding>()),
            startedUtc,
            completedUtc);
    }

    public static DoctorScanResult Failed(
        DoctorFinding finding,
        TimeSpan duration)
    {
        ValidateDuration(duration);
        return Failed(
            finding,
            DateTime.UnixEpoch,
            DateTime.UnixEpoch + duration);
    }

    public static DoctorScanResult Failed(
        DoctorFinding finding,
        DateTime startedUtc,
        DateTime completedUtc)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ValidateTimestamps(startedUtc, completedUtc);

        if (finding.Severity != DoctorSeverity.Error)
        {
            throw new ArgumentException(
                "A failed Doctor scan requires an Error finding.",
                nameof(finding));
        }

        return new DoctorScanResult(
            DoctorScanStatus.Failed,
            null,
            Array.AsReadOnly([finding]),
            startedUtc,
            completedUtc);
    }

    private static IReadOnlyList<DoctorFinding> Snapshot(
        IEnumerable<DoctorFinding>? findings)
    {
        var snapshot = findings?.ToArray() ?? [];

        if (snapshot.Any(finding => finding is null))
        {
            throw new ArgumentException(
                "Findings cannot contain null elements.",
                nameof(findings));
        }

        var unique = new List<DoctorFinding>(snapshot.Length);
        var keys = new HashSet<FindingKey>(FindingKeyComparer.Instance);

        foreach (var finding in snapshot)
        {
            var key = new FindingKey(
                finding.Code,
                NormalizePath(finding.SourcePath),
                finding.Evidence);

            if (keys.Add(key))
                unique.Add(finding);
        }

        return Array.AsReadOnly(unique.ToArray());
    }

    private static void ValidateDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Scan duration cannot be negative.");
    }

    private static void ValidateTimestamps(
        DateTime startedUtc,
        DateTime completedUtc)
    {
        if (startedUtc == default || startedUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Scan start must be a non-default UTC timestamp.", nameof(startedUtc));

        if (completedUtc == default || completedUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Scan completion must be a non-default UTC timestamp.", nameof(completedUtc));

        if (completedUtc < startedUtc)
        {
            throw new ArgumentException(
                "Scan completion cannot precede scan start.",
                nameof(completedUtc));
        }
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var trimmed = path.Trim();

        try
        {
            trimmed = Path.GetFullPath(trimmed);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            // Invalid source paths remain comparable as safe display strings.
        }

        return Path
            .TrimEndingDirectorySeparator(trimmed)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private readonly record struct FindingKey(
        string Code,
        string? SourcePath,
        string Evidence);

    private sealed class FindingKeyComparer : IEqualityComparer<FindingKey>
    {
        public static FindingKeyComparer Instance { get; } = new();

        public bool Equals(FindingKey x, FindingKey y)
        {
            return string.Equals(x.Code, y.Code, StringComparison.Ordinal)
                && string.Equals(x.SourcePath, y.SourcePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Evidence, y.Evidence, StringComparison.Ordinal);
        }

        public int GetHashCode(FindingKey obj)
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.Code),
                obj.SourcePath is null
                    ? 0
                    : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SourcePath),
                StringComparer.Ordinal.GetHashCode(obj.Evidence));
        }
    }
}
