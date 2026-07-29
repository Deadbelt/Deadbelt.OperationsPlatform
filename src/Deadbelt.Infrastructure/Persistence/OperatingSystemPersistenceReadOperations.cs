namespace Deadbelt.Infrastructure.Persistence;

internal sealed class OperatingSystemPersistenceReadOperations
    : IPersistenceReadOperations
{
    public Stream OpenRead(string path)
    {
        return File.OpenRead(path);
    }

    public IReadOnlyList<string> EnumerateDirectories(string path)
    {
        return Directory
            .EnumerateDirectories(path)
            .ToArray();
    }
}
