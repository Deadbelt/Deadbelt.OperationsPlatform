namespace Deadbelt.Application.Persistence;

public sealed class PersistenceDiagnostic
{
    public const string UnknownSourcePath = "<unknown>";

    public PersistenceDiagnostic(
        string code,
        PersistenceDiagnosticSeverity severity,
        PersistenceResourceCategory resourceCategory,
        string sourcePath,
        string message)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Diagnostic code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Diagnostic source path is required.", nameof(sourcePath));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Diagnostic message is required.", nameof(message));

        Code = code.Trim();
        Severity = severity;
        ResourceCategory = resourceCategory;
        SourcePath = sourcePath.Trim();
        Message = message.Trim();
    }

    public string Code { get; }

    public PersistenceDiagnosticSeverity Severity { get; }

    public PersistenceResourceCategory ResourceCategory { get; }

    public string SourcePath { get; }

    public string Message { get; }
}
