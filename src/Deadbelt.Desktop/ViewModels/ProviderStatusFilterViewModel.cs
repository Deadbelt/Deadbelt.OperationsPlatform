using Deadbelt.Domain.Providers;

namespace Deadbelt.Desktop.ViewModels;

public sealed class ProviderStatusFilterViewModel
{
    private ProviderStatusFilterViewModel(
        string displayName,
        ProviderStatus? status)
    {
        DisplayName = displayName;
        Status = status;
    }

    public string DisplayName { get; }

    public ProviderStatus? Status { get; }

    public bool Matches(ProviderSummaryViewModel provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return Status is null
            || provider.Status == Status.Value;
    }

    public static IReadOnlyList<ProviderStatusFilterViewModel> CreateDefaultFilters()
    {
        return new[]
        {
            new ProviderStatusFilterViewModel("All", null),
            new ProviderStatusFilterViewModel("Draft", ProviderStatus.Draft),
            new ProviderStatusFilterViewModel("Configured", ProviderStatus.Configured),
            new ProviderStatusFilterViewModel("Disabled", ProviderStatus.Disabled),
            new ProviderStatusFilterViewModel("Error", ProviderStatus.Error),
            new ProviderStatusFilterViewModel("Archived", ProviderStatus.Archived)
        };
    }
}
