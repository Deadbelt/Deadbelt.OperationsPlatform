using Deadbelt.Application.Persistence;
using Deadbelt.Application.Workspaces;
using Deadbelt.Domain.Workspaces;

namespace Deadbelt.Application.Tests;

public sealed class PersistenceResultTests
{
    [Fact]
    public void SuccessfulLoadAcceptsWarningsAndDefensivelyCopiesDiagnostics()
    {
        var warnings = new List<PersistenceDiagnostic>
        {
            CreateDiagnostic(PersistenceDiagnosticSeverity.Warning)
        };

        var result = PersistenceLoadResult<IReadOnlyList<string>>.Success(
            Array.Empty<string>(),
            warnings);
        warnings.Clear();

        Assert.False(result.HasBlockingErrors);
        Assert.Single(result.Diagnostics);
        Assert.Throws<NotSupportedException>(
            () => ((IList<PersistenceDiagnostic>)result.Diagnostics).Clear());
    }

    [Fact]
    public void SuccessfulLoadRejectsBlockingErrors()
    {
        var error = CreateDiagnostic(PersistenceDiagnosticSeverity.Error);

        Assert.Throws<ArgumentException>(
            () => PersistenceLoadResult<string>.Success(
                "value",
                [error]));
    }

    [Fact]
    public void BlockingLoadRequiresErrorAndExposesNoValue()
    {
        Assert.Throws<ArgumentException>(
            () => PersistenceLoadResult<Workspace?>.BlockingFailure(
                [CreateDiagnostic(PersistenceDiagnosticSeverity.Warning)]));

        var diagnostics = new List<PersistenceDiagnostic>
        {
            CreateDiagnostic(PersistenceDiagnosticSeverity.Error)
        };
        var result = PersistenceLoadResult<Workspace?>.BlockingFailure(
            diagnostics);
        diagnostics.Clear();

        Assert.Null(result.Value);
        Assert.True(result.HasBlockingErrors);
        Assert.Single(result.Diagnostics);
    }

    [Fact]
    public void SuccessfulWorkspaceResultRejectsBlockingErrors()
    {
        Assert.Throws<ArgumentException>(
            () => OpenWorkspaceResult.Success(
                CreateWorkspace(),
                [CreateDiagnostic(PersistenceDiagnosticSeverity.Error)]));
    }

    [Fact]
    public void SuccessfulWorkspaceResultDefensivelyCopiesWarnings()
    {
        var diagnostics = new List<PersistenceDiagnostic>
        {
            CreateDiagnostic(PersistenceDiagnosticSeverity.Warning)
        };

        var result = OpenWorkspaceResult.Success(
            CreateWorkspace(),
            diagnostics);
        diagnostics.Clear();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Workspace);
        Assert.Single(result.Diagnostics);
        Assert.Throws<NotSupportedException>(
            () => ((IList<PersistenceDiagnostic>)result.Diagnostics).Clear());
    }

    [Fact]
    public void BlockingWorkspaceResultRequiresErrorAndDefensivelyCopiesDiagnostics()
    {
        Assert.Throws<ArgumentException>(
            () => OpenWorkspaceResult.BlockingFailure(
                "Blocked.",
                [CreateDiagnostic(PersistenceDiagnosticSeverity.Warning)]));

        var diagnostics = new List<PersistenceDiagnostic>
        {
            CreateDiagnostic(PersistenceDiagnosticSeverity.Error)
        };
        var result = OpenWorkspaceResult.BlockingFailure(
            "Blocked.",
            diagnostics);
        diagnostics.Clear();

        Assert.False(result.Succeeded);
        Assert.Null(result.Workspace);
        Assert.Equal("Blocked.", result.ErrorMessage);
        Assert.Single(result.Diagnostics);
        Assert.Throws<NotSupportedException>(
            () => ((IList<PersistenceDiagnostic>)result.Diagnostics).Clear());
    }

    [Fact]
    public void ValidationFailureCannotCarryPersistenceDiagnostics()
    {
        var result = OpenWorkspaceResult.Failure("Invalid request.");

        Assert.False(result.Succeeded);
        Assert.Null(result.Workspace);
        Assert.Empty(result.Diagnostics);
    }

    private static PersistenceDiagnostic CreateDiagnostic(
        PersistenceDiagnosticSeverity severity)
    {
        return new PersistenceDiagnostic(
            "DOP.Persistence.Test",
            severity,
            PersistenceResourceCategory.Workspace,
            "C:\\workspace\\workspace.json",
            "Safe diagnostic message.");
    }

    private static Workspace CreateWorkspace()
    {
        return new Workspace(
            "Operations",
            "C:\\workspace",
            null,
            new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            "0.1");
    }
}
