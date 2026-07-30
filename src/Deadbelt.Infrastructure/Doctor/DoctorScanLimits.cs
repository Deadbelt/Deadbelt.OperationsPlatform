namespace Deadbelt.Infrastructure.Doctor;

/// <summary>
/// Immutable safety limits for one local Doctor scan.
/// </summary>
internal sealed class DoctorScanLimits
{
    public DoctorScanLimits(
        int maximumRecursionDepth,
        int maximumEnumeratedEntries,
        int maximumFindings,
        int maximumInventoryEntries,
        long maximumStartupBytes,
        long maximumConfigurationBytes,
        long maximumMetadataBytes,
        long maximumMissionDocumentBytes)
    {
        if (maximumRecursionDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumRecursionDepth));

        if (maximumEnumeratedEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumEnumeratedEntries));

        if (maximumFindings <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumFindings));

        if (maximumInventoryEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumInventoryEntries));

        if (maximumStartupBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumStartupBytes));

        if (maximumConfigurationBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumConfigurationBytes));

        if (maximumMetadataBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMetadataBytes));

        if (maximumMissionDocumentBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMissionDocumentBytes));

        MaximumRecursionDepth = maximumRecursionDepth;
        MaximumEnumeratedEntries = maximumEnumeratedEntries;
        MaximumFindings = maximumFindings;
        MaximumInventoryEntries = maximumInventoryEntries;
        MaximumStartupBytes = maximumStartupBytes;
        MaximumConfigurationBytes = maximumConfigurationBytes;
        MaximumMetadataBytes = maximumMetadataBytes;
        MaximumMissionDocumentBytes = maximumMissionDocumentBytes;
    }

    public static DoctorScanLimits Default { get; } = new(
        maximumRecursionDepth: 16,
        maximumEnumeratedEntries: 100_000,
        maximumFindings: 5_000,
        maximumInventoryEntries: 100_000,
        maximumStartupBytes: 1024L * 1024L,
        maximumConfigurationBytes: 2L * 1024L * 1024L,
        maximumMetadataBytes: 1024L * 1024L,
        maximumMissionDocumentBytes: 8L * 1024L * 1024L);

    public int MaximumRecursionDepth { get; }

    public int MaximumEnumeratedEntries { get; }

    public int MaximumFindings { get; }

    public int MaximumInventoryEntries { get; }

    public long MaximumStartupBytes { get; }

    public long MaximumConfigurationBytes { get; }

    public long MaximumMetadataBytes { get; }

    public long MaximumMissionDocumentBytes { get; }
}
