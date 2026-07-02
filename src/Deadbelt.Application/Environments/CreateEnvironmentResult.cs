using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Application.Environments;

public sealed class CreateEnvironmentResult
{
    private CreateEnvironmentResult(
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

    public static CreateEnvironmentResult Success(DOPEnvironment environment)
    {
        return new CreateEnvironmentResult(true, environment, null);
    }

    public static CreateEnvironmentResult Failure(string errorMessage)
    {
        return new CreateEnvironmentResult(false, null, errorMessage);
    }
}