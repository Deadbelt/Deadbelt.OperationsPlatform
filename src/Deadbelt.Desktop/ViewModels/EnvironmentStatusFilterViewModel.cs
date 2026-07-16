using Deadbelt.Domain.Environments;

namespace Deadbelt.Desktop.ViewModels;

public sealed class EnvironmentStatusFilterViewModel
{
    private EnvironmentStatusFilterViewModel(
        string displayName,
        EnvironmentStatus? status)
    {
        DisplayName = displayName;
        Status = status;
    }

    public string DisplayName { get; }

    public EnvironmentStatus? Status { get; }

    public bool IsAll => Status is null;

    public bool Matches(EnvironmentSummaryViewModel environment)
    {
        if (Status is null)
            return true;

        return environment.Status == Status.Value;
    }

    public static IReadOnlyList<EnvironmentStatusFilterViewModel> CreateDefaultFilters()
    {
        return
        [
            new EnvironmentStatusFilterViewModel("All", null),
            new EnvironmentStatusFilterViewModel("Draft", EnvironmentStatus.Draft),
            new EnvironmentStatusFilterViewModel("Active", EnvironmentStatus.Active),
            new EnvironmentStatusFilterViewModel("Disabled", EnvironmentStatus.Disabled),
            new EnvironmentStatusFilterViewModel("Archived", EnvironmentStatus.Archived)
        ];
    }
}
