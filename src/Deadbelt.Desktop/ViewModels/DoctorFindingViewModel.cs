using Deadbelt.Domain.Doctor;

namespace Deadbelt.Desktop.ViewModels;

public sealed class DoctorFindingViewModel
{
    private DoctorFindingViewModel(DoctorFinding finding)
    {
        Code = finding.Code;
        Severity = finding.Severity.ToString();
        Title = finding.Title;
        Explanation = finding.Explanation;
        Evidence = finding.Evidence;
        Recommendation = finding.Recommendation;
        SourcePath = finding.SourcePath ?? string.Empty;
    }

    public string Code { get; }

    public string Severity { get; }

    public string Title { get; }

    public string Explanation { get; }

    public string Evidence { get; }

    public string Recommendation { get; }

    public string SourcePath { get; }

    public static DoctorFindingViewModel FromFinding(DoctorFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return new DoctorFindingViewModel(finding);
    }
}
