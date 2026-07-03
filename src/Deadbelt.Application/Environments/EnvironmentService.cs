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

            var environmentPathExists = await _environmentStore.EnvironmentPathExistsAsync(
                environmentPath,
                cancellationToken);

            if (environmentPathExists)
            {
                return CreateEnvironmentResult.Failure(
                    "An environment with this name already exists in the current workspace.");
            }

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

    public async Task<UpdateEnvironmentResult> UpdateEnvironmentAsync(
    UpdateEnvironmentRequest request,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspacePath))
            return UpdateEnvironmentResult.Failure("Workspace path is required.");

        if (!PathValidator.IsValidFullyQualifiedFolderPath(request.WorkspacePath))
            return UpdateEnvironmentResult.Failure("Workspace path must be a valid full path.");

        if (request.EnvironmentId == Guid.Empty)
            return UpdateEnvironmentResult.Failure("Environment ID is required.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return UpdateEnvironmentResult.Failure("Environment name is required.");

        if (!Enum.IsDefined(request.GameType) || request.GameType == GameType.Unknown)
            return UpdateEnvironmentResult.Failure("Environment game type is invalid.");

        try
        {
            var environments = await _environmentStore.LoadByWorkspaceAsync(
                request.WorkspacePath,
                cancellationToken);

            var currentEnvironment = environments.FirstOrDefault(
                environment => environment.Id.Value == request.EnvironmentId);

            if (currentEnvironment is null)
                return UpdateEnvironmentResult.Failure("Environment was not found.");

            var requestedSafeFolderName = ToSafeFolderName(request.Name);
            var requestedEnvironmentPath = BuildEnvironmentPath(
                request.WorkspacePath,
                request.Name);

            var duplicateEnvironmentExists = environments.Any(environment =>
                environment.Id.Value != request.EnvironmentId
                && (
                    string.Equals(
                        ToSafeFolderName(environment.Name),
                        requestedSafeFolderName,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        environment.EnvironmentPath,
                        requestedEnvironmentPath,
                        StringComparison.OrdinalIgnoreCase)
                ));

            if (duplicateEnvironmentExists)
            {
                return UpdateEnvironmentResult.Failure(
                    "An environment with this name already exists in the current workspace.");
            }

            var updatedEnvironment = new DOPEnvironment(
                EnvironmentId.From(request.EnvironmentId),
                currentEnvironment.WorkspacePath,
                request.Name,
                request.Description,
                request.GameType,
                currentEnvironment.EnvironmentPath,
                currentEnvironment.CreatedUtc,
                currentEnvironment.Version,
                currentEnvironment.Status);

            await _environmentStore.UpdateAsync(
                updatedEnvironment,
                cancellationToken);

            _logger.LogInformation(
                "Environment updated: {EnvironmentName} at {EnvironmentPath}",
                updatedEnvironment.Name,
                updatedEnvironment.EnvironmentPath);

            return UpdateEnvironmentResult.Success(updatedEnvironment);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Environment update validation failed.");

            return UpdateEnvironmentResult.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update environment.");

            return UpdateEnvironmentResult.Failure(
                "Failed to update environment. See logs for details.");
        }
    }
    public async Task<IReadOnlyList<DOPEnvironment>> LoadByWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            _logger.LogWarning("Unable to load environments because workspace path is empty.");
            return Array.Empty<DOPEnvironment>();
        }

        if (!PathValidator.IsValidFullyQualifiedFolderPath(workspacePath))
        {
            _logger.LogWarning(
                "Unable to load environments because workspace path is invalid: {WorkspacePath}",
                workspacePath);

            return Array.Empty<DOPEnvironment>();
        }

        try
        {
            var environments = await _environmentStore.LoadByWorkspaceAsync(
                workspacePath,
                cancellationToken);

            _logger.LogInformation(
                "Loaded {EnvironmentCount} environments from workspace {WorkspacePath}.",
                environments.Count,
                workspacePath);

            return environments;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load environments from workspace {WorkspacePath}.",
                workspacePath);

            return Array.Empty<DOPEnvironment>();
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
