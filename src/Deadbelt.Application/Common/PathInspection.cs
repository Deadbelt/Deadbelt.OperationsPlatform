namespace Deadbelt.Application.Common;

public static class PathInspection
{
    public static bool IsValidFullyQualifiedFolderPath(
        IPathInspector pathInspector,
        string folderPath)
    {
        ArgumentNullException.ThrowIfNull(pathInspector);

        try
        {
            return pathInspector.IsValidFullyQualifiedFolderPath(folderPath);
        }
        catch
        {
            return false;
        }
    }

    public static bool DirectoryExists(
        IPathInspector pathInspector,
        string folderPath)
    {
        ArgumentNullException.ThrowIfNull(pathInspector);

        try
        {
            return pathInspector.DirectoryExists(folderPath);
        }
        catch
        {
            return false;
        }
    }
}
