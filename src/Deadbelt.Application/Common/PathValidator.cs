using System.IO;

namespace Deadbelt.Application.Common;

public static class PathValidator
{
    public static bool IsValidFullyQualifiedFolderPath(string folderPath)
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
}