using Microsoft.Extensions.Logging;

namespace Deadbelt.Infrastructure.Tests.TestSupport;

internal sealed class RecordingLogger : ILogger
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add(
            new LogEntry(
                logLevel,
                exception,
                formatter(state, exception)));
    }

    internal sealed record LogEntry(
        LogLevel Level,
        Exception? Exception,
        string Message);
}
