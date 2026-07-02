namespace Deadbelt.Application.Environments;

public interface IEnvironmentService
{
    Task<CreateEnvironmentResult> CreateEnvironmentAsync(
        CreateEnvironmentRequest request,
        CancellationToken cancellationToken = default);
}