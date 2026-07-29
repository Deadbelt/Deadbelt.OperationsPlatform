namespace Deadbelt.Infrastructure.Persistence;

internal sealed class OperatingSystemAtomicFileOperations : IAtomicFileOperations
{
    public FileStream CreateTemporaryFile(string temporaryFilePath)
    {
        return new FileStream(
            temporaryFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public void MoveNewFile(
        string sourcePath,
        string destinationPath)
    {
        File.Move(
            sourcePath,
            destinationPath,
            overwrite: false);
    }

    public void ReplaceFile(
        string sourcePath,
        string destinationPath)
    {
        File.Replace(
            sourcePath,
            destinationPath,
            destinationBackupFileName: null);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }
}
