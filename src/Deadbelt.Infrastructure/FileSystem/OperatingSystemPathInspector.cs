using Deadbelt.Application.Common;

namespace Deadbelt.Infrastructure.FileSystem;

public sealed class OperatingSystemPathInspector : IPathInspector
{
    public bool IsValidFullyQualifiedFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        try
        {
            var trimmedPath = folderPath.Trim();

            if (!Path.IsPathFullyQualified(trimmedPath))
                return false;

            var root = Path.GetPathRoot(trimmedPath);

            if (string.IsNullOrWhiteSpace(root))
                return false;

            if (!Directory.Exists(root))
                return false;

            var invalidPathChars = Path.GetInvalidPathChars();

            return trimmedPath.IndexOfAny(invalidPathChars) < 0;
        }
        catch
        {
            return false;
        }
    }

    public bool DirectoryExists(string folderPath)
    {
        try
        {
            return Directory.Exists(folderPath);
        }
        catch
        {
            return false;
        }
    }
}
