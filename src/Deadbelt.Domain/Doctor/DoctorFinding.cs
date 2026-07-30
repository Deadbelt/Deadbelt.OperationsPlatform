namespace Deadbelt.Domain.Doctor;

public sealed class DoctorFinding
{
    public DoctorFinding(
        string code,
        DoctorSeverity severity,
        string title,
        string explanation,
        string evidence,
        string recommendation,
        string? sourcePath = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Finding code is required.", nameof(code));

        if (!Enum.IsDefined(severity))
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Invalid Doctor severity.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Finding title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(explanation))
            throw new ArgumentException("Finding explanation is required.", nameof(explanation));

        if (string.IsNullOrWhiteSpace(evidence))
            throw new ArgumentException("Finding evidence is required.", nameof(evidence));

        if (string.IsNullOrWhiteSpace(recommendation))
            throw new ArgumentException("Finding recommendation is required.", nameof(recommendation));

        Code = code.Trim();
        Severity = severity;
        Title = title.Trim();
        Explanation = explanation.Trim();
        Evidence = evidence.Trim();
        Recommendation = recommendation.Trim();
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? null
            : sourcePath.Trim();
    }

    public string Code { get; }

    public DoctorSeverity Severity { get; }

    public string Title { get; }

    public string Explanation { get; }

    public string Evidence { get; }

    public string Recommendation { get; }

    public string? SourcePath { get; }
}
