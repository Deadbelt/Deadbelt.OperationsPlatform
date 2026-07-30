namespace Deadbelt.Infrastructure.Doctor;

internal enum DoctorFileSystemStatus
{
    Available = 0,
    Missing = 1,
    Unreadable = 2,
    InvalidPath = 3,
    TooLarge = 4,
    Cancelled = 5
}

internal sealed record DoctorPathInspection(
    DoctorFileSystemStatus Status,
    FileAttributes Attributes = 0,
    long? FileSize = null,
    DateTime? LastModifiedUtc = null)
{
    public bool IsAvailable => Status == DoctorFileSystemStatus.Available;

    public bool IsReparsePoint =>
        IsAvailable && Attributes.HasFlag(FileAttributes.ReparsePoint);
}

internal sealed record DoctorTextReadResult(
    DoctorFileSystemStatus Status,
    string? Content = null,
    long? DetectedSize = null);

internal sealed record DoctorFileSystemEntry(
    string FullPath,
    string Name,
    bool IsDirectory,
    DoctorFileSystemStatus Status,
    FileAttributes Attributes,
    long? FileSize,
    DateTime? LastModifiedUtc)
{
    public bool IsReparsePoint =>
        Status == DoctorFileSystemStatus.Available
        && Attributes.HasFlag(FileAttributes.ReparsePoint);
}

internal sealed record DoctorDirectoryEnumerationResult(
    DoctorFileSystemStatus Status,
    IReadOnlyList<DoctorFileSystemEntry> Entries,
    bool LimitReached = false);
