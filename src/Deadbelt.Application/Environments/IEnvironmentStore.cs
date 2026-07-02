using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Application.Environments;

public interface IEnvironmentStore
{
    Task SaveAsync(
        DOPEnvironment environment,
        CancellationToken cancellationToken = default);
}