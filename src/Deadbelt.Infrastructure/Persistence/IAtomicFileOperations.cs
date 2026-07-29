namespace Deadbelt.Infrastructure.Persistence;

internal interface IAtomicFileOperations
{
    FileStream CreateTemporaryFile(string temporaryFilePath);

    bool FileExists(string path);

    void MoveNewFile(
        string sourcePath,
        string destinationPath);

    void ReplaceFile(
        string sourcePath,
        string destinationPath);

    void DeleteFile(string path);
}
