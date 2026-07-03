using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Application.Environments;

public sealed class RestoreEnvironmentResult
{
    private RestoreEnvironmentResult(
        bool succeeded,
        DOPEnvironment? environment,
        string? errorMessage)
    {
        Succeeded = succeeded;
        Environment = environment;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public DOPEnvironment? Environment { get; }

    public string? ErrorMessage { get; }

    public static RestoreEnvironmentResult Success(DOPEnvironment environment)
    {
        return new RestoreEnvironmentResult(true, environment, null);
    }

    public static RestoreEnvironmentResult Failure(string errorMessage)
    {
        return new RestoreEnvironmentResult(false, null, errorMessage);
    }
}