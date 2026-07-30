using Deadbelt.Infrastructure.Doctor;
using Deadbelt.Infrastructure.Tests.TestSupport;

namespace Deadbelt.Infrastructure.Tests;

public sealed class OperatingSystemDoctorFileSystemTests
{
    [Fact]
    public void MissingAndInvalidPathsHaveDistinctStructuredOutcomes()
    {
        using var temporary = new TemporaryDirectory();
        var fileSystem = new OperatingSystemDoctorFileSystem();

        var missingFile = fileSystem.InspectFile(
            temporary.GetPath("missing.txt"),
            CancellationToken.None);
        var missingDirectory = fileSystem.InspectDirectory(
            temporary.GetPath("missing"),
            CancellationToken.None);
        var invalid = fileSystem.InspectFile(
            "\0invalid",
            CancellationToken.None);

        Assert.Equal(DoctorFileSystemStatus.Missing, missingFile.Status);
        Assert.Equal(DoctorFileSystemStatus.Missing, missingDirectory.Status);
        Assert.Equal(DoctorFileSystemStatus.InvalidPath, invalid.Status);
    }

    [Fact]
    public void BoundedReadRejectsOversizedFileWithoutReturningContent()
    {
        using var temporary = new TemporaryDirectory();
        var path = temporary.GetPath("large.txt");
        File.WriteAllText(path, "12345");
        var fileSystem = new OperatingSystemDoctorFileSystem();

        var result = fileSystem.ReadText(
            path,
            maximumBytes: 4,
            CancellationToken.None);

        Assert.Equal(DoctorFileSystemStatus.TooLarge, result.Status);
        Assert.Null(result.Content);
        Assert.Equal(5, result.DetectedSize);
    }

    [Fact]
    public void DirectoryEnumerationIsTopLevelAndBounded()
    {
        using var temporary = new TemporaryDirectory();
        File.WriteAllText(temporary.GetPath("one.txt"), "one");
        File.WriteAllText(temporary.GetPath("two.txt"), "two");
        Directory.CreateDirectory(temporary.GetPath("nested"));
        File.WriteAllText(
            temporary.GetPath("nested", "not-top-level.txt"),
            "nested");
        var fileSystem = new OperatingSystemDoctorFileSystem();

        var result = fileSystem.EnumerateDirectory(
            temporary.Path,
            maximumEntries: 2,
            CancellationToken.None);

        Assert.Equal(DoctorFileSystemStatus.Available, result.Status);
        Assert.Equal(2, result.Entries.Count);
        Assert.True(result.LimitReached);
        Assert.DoesNotContain(
            result.Entries,
            entry => entry.FullPath.EndsWith(
                "not-top-level.txt",
                StringComparison.OrdinalIgnoreCase));
    }
}
