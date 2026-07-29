namespace Deadbelt.Infrastructure.Persistence;

internal interface IPersistenceReadOperations
{
    Stream OpenRead(string path);

    IReadOnlyList<string> EnumerateDirectories(string path);
}
