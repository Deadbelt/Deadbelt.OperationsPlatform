using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Application.Environments;

public sealed class ArchiveEnvironmentResult
{
    private ArchiveEnvironmentResult(
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

    public static ArchiveEnvironmentResult Success(DOPEnvironment environment)
    {
        return new ArchiveEnvironmentResult(true, environment, null);
    }

    public static ArchiveEnvironmentResult Failure(string errorMessage)
    {
        return new ArchiveEnvironmentResult(false, null, errorMessage);
    }
}