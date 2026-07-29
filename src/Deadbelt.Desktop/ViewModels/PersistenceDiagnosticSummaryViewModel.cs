using Deadbelt.Application.Persistence;

namespace Deadbelt.Desktop.ViewModels;

public sealed class PersistenceDiagnosticSummaryViewModel
{
    private PersistenceDiagnosticSummaryViewModel(
        string code,
        string resource,
        string sourcePath,
        string message)
    {
        Code = code;
        Resource = resource;
        SourcePath = sourcePath;
        Message = message;
    }

    public string Code { get; }

    public string Resource { get; }

    public string SourcePath { get; }

    public string Message { get; }

    public static PersistenceDiagnosticSummaryViewModel FromDiagnostic(
        PersistenceDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return new PersistenceDiagnosticSummaryViewModel(
            diagnostic.Code,
            diagnostic.ResourceCategory.ToString(),
            diagnostic.SourcePath,
            diagnostic.Message);
    }
}
