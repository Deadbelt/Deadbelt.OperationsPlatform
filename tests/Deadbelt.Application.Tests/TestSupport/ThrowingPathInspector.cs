using Deadbelt.Application.Common;

namespace Deadbelt.Application.Tests.TestSupport;

internal sealed class ThrowingPathInspector : IPathInspector
{
    private const string ExceptionMessage = "Deterministic path inspection failure.";

    public bool IsValidFullyQualifiedFolderPath(string folderPath)
    {
        throw new InvalidOperationException(ExceptionMessage);
    }

    public bool DirectoryExists(string folderPath)
    {
        throw new InvalidOperationException(ExceptionMessage);
    }
}
