namespace Deadbelt.Application.Common;

/// <summary>
/// Provides non-throwing, read-only inspection of paths using the current
/// platform's path and filesystem semantics.
/// </summary>
/// <remarks>
/// Implementations must not propagate filesystem or path-inspection exceptions.
/// </remarks>
public interface IPathInspector
{
    /// <summary>
    /// Determines whether a path represents an inspectable, fully qualified
    /// folder path using the current platform's path semantics.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when <paramref name="folderPath"/> is
    /// <see langword="null"/>, empty, whitespace, malformed,
    /// non-fully-qualified, inaccessible, or otherwise cannot be inspected.
    /// </returns>
    bool IsValidFullyQualifiedFolderPath(string folderPath);

    /// <summary>
    /// Determines whether an inspectable directory exists at the supplied path.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the directory does not exist or
    /// <paramref name="folderPath"/> cannot be inspected.
    /// </returns>
    bool DirectoryExists(string folderPath);
}
