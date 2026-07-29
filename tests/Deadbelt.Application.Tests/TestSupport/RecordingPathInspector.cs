using Deadbelt.Application.Common;

namespace Deadbelt.Application.Tests.TestSupport;

internal sealed class RecordingPathInspector : IPathInspector
{
    public bool IsValidFullyQualifiedFolderPathResult { get; set; }

    public bool DirectoryExistsResult { get; set; }

    public List<string> FullyQualifiedFolderPaths { get; } = [];

    public List<string> DirectoryPaths { get; } = [];

    public bool IsValidFullyQualifiedFolderPath(string folderPath)
    {
        FullyQualifiedFolderPaths.Add(folderPath);
        return IsValidFullyQualifiedFolderPathResult;
    }

    public bool DirectoryExists(string folderPath)
    {
        DirectoryPaths.Add(folderPath);
        return DirectoryExistsResult;
    }
}
