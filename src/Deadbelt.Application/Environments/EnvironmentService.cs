using System.Text;
using Deadbelt.Application.Common;
using Deadbelt.Domain.Environments;
using Microsoft.Extensions.Logging;
using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Application.Environments;

public sealed class EnvironmentService : IEnvironmentService
{
    private const string CurrentEnvironmentVersion = "0.1";

    private readonly IEnvironmentStore _environmentStore;
    private readonly ILogger<EnvironmentService> _logger;

    public EnvironmentService(
        IEnvironmentStore environmentStore,
        ILogger<EnvironmentService> logger)
    {
        _environmentStore = environmentStore;
        _logger = logger;
    }

    public async Task<CreateEnvironmentResult> CreateEnvironmentAsync(
        CreateEnvironmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspacePath))
            return CreateEnvironmentResult.Failure("Workspace path is required.");

        if (!PathValidator.IsValidFullyQualifiedFolderPath(request.WorkspacePath))
            return CreateEnvironmentResult.Failure("Workspace path must be a valid full path.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return CreateEnvironmentResult.Failure("Environment name is required.");

        if (!Enum.IsDefined(request.GameType))
            return CreateEnvironmentResult.Failure("Environment game type is invalid.");

        try
        {
            var environmentPath = BuildEnvironmentPath(
                request.WorkspacePath,
                request.Name);

            var environment = new DOPEnvironment(
                EnvironmentId.New(),
                request.WorkspacePath,
                request.Name,
                request.Description,
                request.GameType,
                environmentPath,
                DateTime.UtcNow,
                CurrentEnvironmentVersion,
                EnvironmentStatus.Draft);

            await _environmentStore.SaveAsync(
                environment,
                cancellationToken);

            _logger.LogInformation(
                "Environment created: {EnvironmentName} at {EnvironmentPath}",
                environment.Name,
                environment.EnvironmentPath);

            return CreateEnvironmentResult.Success(environment);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Environment creation validation failed.");

            return CreateEnvironmentResult.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create environment.");

            return CreateEnvironmentResult.Failure(
                "Failed to create environment. See logs for details.");
        }
    }

    private static string BuildEnvironmentPath(
        string workspacePath,
        string environmentName)
    {
        var safeFolderName = ToSafeFolderName(environmentName);

        return Path.Combine(
            workspacePath,
            "environments",
            safeFolderName);
    }

    private static string ToSafeFolderName(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (character is '-' or '_' or ' ')
            {
                builder.Append('-');
            }
        }

        var result = builder
            .ToString()
            .Trim('-');

        while (result.Contains("--", StringComparison.Ordinal))
        {
            result = result.Replace("--", "-", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("Environment name must contain at least one valid folder character.");

        return result;
    }
}