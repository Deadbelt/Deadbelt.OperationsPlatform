using Deadbelt.Infrastructure.Persistence;

namespace Deadbelt.Infrastructure.Tests.TestSupport;

internal sealed class FaultInjectingPersistenceReadOperations
    : IPersistenceReadOperations
{
    private readonly Dictionary<string, Exception> _openFailures =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _enumerationFailures =
        new(StringComparer.OrdinalIgnoreCase);

    public void FailOpen(
        string path,
        Exception exception)
    {
        _openFailures[path] = exception;
    }

    public void FailEnumeration(
        string path,
        Exception exception)
    {
        _enumerationFailures[path] = exception;
    }

    public Stream OpenRead(string path)
    {
        if (_openFailures.TryGetValue(path, out var exception))
            throw exception;

        return File.OpenRead(path);
    }

    public IReadOnlyList<string> EnumerateDirectories(string path)
    {
        if (_enumerationFailures.TryGetValue(path, out var exception))
            throw exception;

        return Directory
            .EnumerateDirectories(path)
            .ToArray();
    }
}
