using Deadbelt.Infrastructure.Doctor;

namespace Deadbelt.Infrastructure.Tests.TestSupport;

internal sealed class FaultInjectingDoctorFileSystem : IDoctorFileSystem
{
    private readonly IDoctorFileSystem _inner = new OperatingSystemDoctorFileSystem();

    public string? ThrowWhenInspectingPath { get; set; }

    public Action? BeforeEnumerateFiles { get; set; }

    public Func<string, DoctorPathInspection?>? InspectFileOverride { get; set; }

    public Func<string, DoctorPathInspection?>? InspectDirectoryOverride { get; set; }

    public Func<string, long, DoctorTextReadResult?>? ReadTextOverride { get; set; }

    public Func<string, int, DoctorDirectoryEnumerationResult?>? EnumerationOverride { get; set; }

    public DoctorPathInspection InspectFile(
        string path,
        CancellationToken cancellationToken)
    {
        ThrowIfConfigured(path);
        return InspectFileOverride?.Invoke(path)
            ?? _inner.InspectFile(path, cancellationToken);
    }

    public DoctorPathInspection InspectDirectory(
        string path,
        CancellationToken cancellationToken)
    {
        ThrowIfConfigured(path);
        return InspectDirectoryOverride?.Invoke(path)
            ?? _inner.InspectDirectory(path, cancellationToken);
    }

    public DoctorTextReadResult ReadText(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        ThrowIfConfigured(path);
        return ReadTextOverride?.Invoke(path, maximumBytes)
            ?? _inner.ReadText(path, maximumBytes, cancellationToken);
    }

    public DoctorDirectoryEnumerationResult EnumerateDirectory(
        string path,
        int maximumEntries,
        CancellationToken cancellationToken)
    {
        ThrowIfConfigured(path);
        BeforeEnumerateFiles?.Invoke();
        return EnumerationOverride?.Invoke(path, maximumEntries)
            ?? _inner.EnumerateDirectory(path, maximumEntries, cancellationToken);
    }

    private void ThrowIfConfigured(string path)
    {
        if (ThrowWhenInspectingPath is not null
            && path.StartsWith(
                ThrowWhenInspectingPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "Deterministic test-only access failure.");
        }
    }
}
