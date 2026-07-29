using Deadbelt.Infrastructure.FileSystem;
using Deadbelt.Infrastructure.Tests.TestSupport;

namespace Deadbelt.Infrastructure.Tests;

public sealed class OperatingSystemPathInspectorTests
{
    private readonly OperatingSystemPathInspector _inspector = new();

    [Fact]
    public void ExistingDirectoryIsReportedAsExistingAndValid()
    {
        using var temporaryDirectory = new TemporaryDirectory();

        Assert.True(_inspector.DirectoryExists(temporaryDirectory.Path));
        Assert.True(_inspector.IsValidFullyQualifiedFolderPath(temporaryDirectory.Path));
    }

    [Fact]
    public void MissingDirectoryIsNotReportedAsExistingButItsFullPathRemainsValid()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var missingPath = temporaryDirectory.GetPath("missing");

        Assert.False(_inspector.DirectoryExists(missingPath));
        Assert.True(_inspector.IsValidFullyQualifiedFolderPath(missingPath));
    }

    [Fact]
    public void FullyQualifiedPathValidationTrimsSurroundingWhitespace()
    {
        using var temporaryDirectory = new TemporaryDirectory();

        Assert.True(
            _inspector.IsValidFullyQualifiedFolderPath(
                $"  {temporaryDirectory.Path}  "));
    }

    [Fact]
    public void NullPathIsRejected()
    {
        Assert.False(_inspector.DirectoryExists(null!));
        Assert.False(_inspector.IsValidFullyQualifiedFolderPath(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative-folder")]
    [InlineData("invalid\0path")]
    public void CurrentInvalidPathCasesAreRejected(string folderPath)
    {
        Assert.False(_inspector.IsValidFullyQualifiedFolderPath(folderPath));
    }
}
