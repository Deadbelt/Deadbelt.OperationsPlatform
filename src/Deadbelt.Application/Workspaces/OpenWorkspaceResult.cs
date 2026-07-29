using Deadbelt.Application.Persistence;
using Deadbelt.Domain.Workspaces;

namespace Deadbelt.Application.Workspaces;

public sealed class OpenWorkspaceResult
{
    private OpenWorkspaceResult(
        bool succeeded,
        Workspace? workspace,
        string? errorMessage,
        IReadOnlyList<PersistenceDiagnostic> diagnostics)
    {
        Succeeded = succeeded;
        Workspace = workspace;
        ErrorMessage = errorMessage;
        Diagnostics = diagnostics;
    }

    public bool Succeeded { get; }

    public Workspace? Workspace { get; }

    public string? ErrorMessage { get; }

    public IReadOnlyList<PersistenceDiagnostic> Diagnostics { get; }

    public static OpenWorkspaceResult Success(
        Workspace workspace,
        IEnumerable<PersistenceDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var diagnosticSnapshot = Snapshot(diagnostics);

        if (diagnosticSnapshot.Any(
                diagnostic =>
                    diagnostic.Severity == PersistenceDiagnosticSeverity.Error))
        {
            throw new ArgumentException(
                "A successful Workspace open cannot contain blocking error diagnostics.",
                nameof(diagnostics));
        }

        return new OpenWorkspaceResult(
            true,
            workspace,
            null,
            diagnosticSnapshot);
    }

    public static OpenWorkspaceResult Failure(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new OpenWorkspaceResult(
            false,
            null,
            errorMessage,
            Array.AsReadOnly(Array.Empty<PersistenceDiagnostic>()));
    }

    public static OpenWorkspaceResult BlockingFailure(
        string errorMessage,
        IEnumerable<PersistenceDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var diagnosticSnapshot = Snapshot(diagnostics);

        if (!diagnosticSnapshot.Any(
                diagnostic =>
                    diagnostic.Severity == PersistenceDiagnosticSeverity.Error))
        {
            throw new ArgumentException(
                "A blocking Workspace failure requires at least one error diagnostic.",
                nameof(diagnostics));
        }

        return new OpenWorkspaceResult(
            false,
            null,
            errorMessage,
            diagnosticSnapshot);
    }

    private static IReadOnlyList<PersistenceDiagnostic> Snapshot(
        IEnumerable<PersistenceDiagnostic>? diagnostics)
    {
        return Array.AsReadOnly(
            diagnostics?.ToArray() ?? []);
    }
}
