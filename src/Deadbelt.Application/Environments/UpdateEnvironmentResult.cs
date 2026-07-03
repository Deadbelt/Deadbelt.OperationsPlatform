using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Application.Environments;

public sealed class UpdateEnvironmentResult
{
    private UpdateEnvironmentResult(
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

    public static UpdateEnvironmentResult Success(DOPEnvironment environment)
    {
        return new UpdateEnvironmentResult(true, environment, null);
    }

    public static UpdateEnvironmentResult Failure(string errorMessage)
    {
        return new UpdateEnvironmentResult(false, null, errorMessage);
    }
}