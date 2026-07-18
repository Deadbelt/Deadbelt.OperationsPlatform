namespace Deadbelt.Application.Providers;

public interface IProviderService
{
    Task<CreateProviderResult> CreateProviderAsync(
        CreateProviderRequest request,
        CancellationToken cancellationToken = default);
}
