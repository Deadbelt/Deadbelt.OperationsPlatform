namespace Deadbelt.Application.Persistence;

public sealed class PersistenceLoadResult<T>
{
    private PersistenceLoadResult(
        T value,
        IReadOnlyList<PersistenceDiagnostic> diagnostics)
    {
        Value = value;
        Diagnostics = diagnostics;
    }

    public T Value { get; }

    public IReadOnlyList<PersistenceDiagnostic> Diagnostics { get; }

    public bool HasDiagnostics => Diagnostics.Count > 0;

    public bool HasBlockingErrors => Diagnostics.Any(
        diagnostic => diagnostic.Severity == PersistenceDiagnosticSeverity.Error);

    public static PersistenceLoadResult<T> Success(
        T value,
        IEnumerable<PersistenceDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        var diagnosticSnapshot = Snapshot(diagnostics);

        if (diagnosticSnapshot.Any(
                diagnostic =>
                    diagnostic.Severity == PersistenceDiagnosticSeverity.Error))
        {
            throw new ArgumentException(
                "A successful persistence load cannot contain blocking error diagnostics.",
                nameof(diagnostics));
        }

        return new PersistenceLoadResult<T>(
            value,
            diagnosticSnapshot);
    }

    public static PersistenceLoadResult<T> BlockingFailure(
        IEnumerable<PersistenceDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var diagnosticSnapshot = Snapshot(diagnostics);

        if (!diagnosticSnapshot.Any(
                diagnostic =>
                    diagnostic.Severity == PersistenceDiagnosticSeverity.Error))
        {
            throw new ArgumentException(
                "A blocking persistence failure requires at least one error diagnostic.",
                nameof(diagnostics));
        }

        return new PersistenceLoadResult<T>(
            default!,
            diagnosticSnapshot);
    }

    private static IReadOnlyList<PersistenceDiagnostic> Snapshot(
        IEnumerable<PersistenceDiagnostic>? diagnostics)
    {
        return Array.AsReadOnly(
            diagnostics?.ToArray() ?? []);
    }
}
