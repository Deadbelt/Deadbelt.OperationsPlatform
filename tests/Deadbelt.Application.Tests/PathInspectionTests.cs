using Deadbelt.Application.Common;
using Deadbelt.Application.Tests.TestSupport;

namespace Deadbelt.Application.Tests;

public sealed class PathInspectionTests
{
    [Fact]
    public void FaultyInspectorExceptionsAreReturnedAsNegativeResults()
    {
        var pathInspector = new ThrowingPathInspector();

        Assert.False(
            PathInspection.IsValidFullyQualifiedFolderPath(
                pathInspector,
                "inspector-failure"));
        Assert.False(
            PathInspection.DirectoryExists(
                pathInspector,
                "inspector-failure"));
    }
}
