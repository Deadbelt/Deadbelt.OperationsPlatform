using System.Text.Json;
using System.Text.Json.Serialization;
using Deadbelt.Application.Environments;
using Deadbelt.Application.Persistence;
using Deadbelt.Domain.Environments;
using Deadbelt.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Infrastructure.Environments;

public sealed class JsonEnvironmentStore : IEnvironmentStore
{
    private const string EnvironmentsFolderName = "environments";
    private const string EnvironmentFileName = "environment.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly ILogger<JsonEnvironmentStore> _logger;
    private readonly IPersistenceReadOperations _readOperations;

    public JsonEnvironmentStore()
        : this(
            NullLogger<JsonEnvironmentStore>.Instance,
            new OperatingSystemPersistenceReadOperations())
    {
    }

    public JsonEnvironmentStore(ILogger<JsonEnvironmentStore> logger)
        : this(
            logger,
            new OperatingSystemPersistenceReadOperations())
    {
    }

    internal JsonEnvironmentStore(
        ILogger<JsonEnvironmentStore> logger,
        IPersistenceReadOperations readOperations)
    {
        _logger = logger;
        _readOperations = readOperations;
    }

    public async Task SaveAsync(
        DOPEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        var environmentFilePath = Path.Combine(
            environment.EnvironmentPath,
            EnvironmentFileName);

        try
        {
            Directory.CreateDirectory(environment.EnvironmentPath);

            if (File.Exists(environmentFilePath))
            {
                throw new InvalidOperationException(
                    $"An environment already exists at '{environment.EnvironmentPath}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to prepare environment metadata write at {EnvironmentFilePath}.",
                environmentFilePath);

            throw;
        }

        await WriteMetadataAsync(
            environment,
            environmentFilePath,
            overwrite: false,
            cancellationToken);
    }

    public async Task UpdateAsync(
        DOPEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        var environmentFilePath = Path.Combine(
            environment.EnvironmentPath,
            EnvironmentFileName);

        try
        {
            Directory.CreateDirectory(environment.EnvironmentPath);

            if (!File.Exists(environmentFilePath))
            {
                throw new InvalidOperationException(
                    $"Environment metadata does not exist at '{environment.EnvironmentPath}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to prepare environment metadata update at {EnvironmentFilePath}.",
                environmentFilePath);

            throw;
        }

        await WriteMetadataAsync(
            environment,
            environmentFilePath,
            overwrite: true,
            cancellationToken);
    }

    public async Task<PersistenceLoadResult<IReadOnlyList<DOPEnvironment>>> LoadByWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        var environmentsRootPath = Path.Combine(
            workspacePath,
            EnvironmentsFolderName);

        IReadOnlyList<string> environmentDirectories;

        try
        {
            environmentDirectories = _readOperations
                .EnumerateDirectories(environmentsRootPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return PersistenceLoadResult<IReadOnlyList<DOPEnvironment>>.Success(
                Array.Empty<DOPEnvironment>());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var diagnostic = CreateDiagnostic(
                PersistenceDiagnosticCodes.EnvironmentCollectionUnreadable,
                environmentsRootPath,
                $"Environment metadata could not be enumerated under '{environmentsRootPath}'.");

            _logger.LogError(
                ex,
                "Failed to enumerate environment metadata under {EnvironmentsRootPath}.",
                environmentsRootPath);

            return PersistenceLoadResult<IReadOnlyList<DOPEnvironment>>.Success(
                Array.Empty<DOPEnvironment>(),
                [diagnostic]);
        }

        var environments = new List<DOPEnvironment>();
        var diagnostics = new List<PersistenceDiagnostic>();

        foreach (var environmentDirectory in environmentDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var environmentFilePath = Path.Combine(
                environmentDirectory,
                EnvironmentFileName);

            var loadResult = await LoadEnvironmentAsync(
                workspacePath,
                environmentFilePath,
                cancellationToken);

            if (loadResult.Environment is not null)
                environments.Add(loadResult.Environment);

            if (loadResult.Diagnostic is not null)
                diagnostics.Add(loadResult.Diagnostic);
        }

        return PersistenceLoadResult<IReadOnlyList<DOPEnvironment>>.Success(
            environments
                .OrderBy(environment => environment.Name)
                .ToArray(),
            diagnostics);
    }

    public Task<bool> EnvironmentPathExistsAsync(
        string environmentPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(environmentPath))
            return Task.FromResult(false);

        var environmentFilePath = Path.Combine(
            environmentPath,
            EnvironmentFileName);

        var exists = Directory.Exists(environmentPath)
            || File.Exists(environmentFilePath);

        return Task.FromResult(exists);
    }

    private async Task WriteMetadataAsync(
        DOPEnvironment environment,
        string environmentFilePath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var metadata = new EnvironmentMetadata
        {
            Id = environment.Id.Value,
            Name = environment.Name,
            Description = environment.Description,
            GameType = environment.GameType,
            EnvironmentPath = environment.EnvironmentPath,
            CreatedUtc = environment.CreatedUtc,
            Version = environment.Version,
            Status = environment.Status
        };

        await AtomicJsonFileWriter.WriteAsync(
            environmentFilePath,
            metadata,
            JsonOptions,
            overwrite,
            _logger,
            cancellationToken);
    }

    private async Task<EnvironmentLoadAttempt> LoadEnvironmentAsync(
        string workspacePath,
        string environmentFilePath,
        CancellationToken cancellationToken)
    {
        var streamOpened = false;

        try
        {
            await using var stream = _readOperations.OpenRead(environmentFilePath);
            streamOpened = true;

            var metadata = await JsonSerializer.DeserializeAsync<EnvironmentMetadata>(
                stream,
                JsonOptions,
                cancellationToken);

            if (metadata is null)
                throw new InvalidDataException("Environment metadata deserialized to null.");

            var environment = new DOPEnvironment(
                EnvironmentId.From(metadata.Id),
                workspacePath,
                metadata.Name,
                metadata.Description,
                metadata.GameType,
                metadata.EnvironmentPath,
                metadata.CreatedUtc,
                metadata.Version,
                metadata.Status);

            return new EnvironmentLoadAttempt(
                environment,
                null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var classification = ClassifyChildReadFailure(
                ex,
                environmentFilePath,
                streamOpened);
            var diagnostic = CreateDiagnostic(
                classification.Code,
                environmentFilePath,
                classification.Message);

            _logger.LogWarning(
                ex,
                "Skipping environment metadata with {DiagnosticCode} at {EnvironmentFilePath}.",
                classification.Code,
                environmentFilePath);

            return new EnvironmentLoadAttempt(
                null,
                diagnostic);
        }
    }

    private static ChildReadFailure ClassifyChildReadFailure(
        Exception exception,
        string environmentFilePath,
        bool streamOpened)
    {
        if (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new ChildReadFailure(
                PersistenceDiagnosticCodes.EnvironmentMetadataMissing,
                $"Environment metadata was not found at '{environmentFilePath}'.");
        }

        if (streamOpened
            && exception is JsonException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException)
        {
            return new ChildReadFailure(
                PersistenceDiagnosticCodes.EnvironmentMetadataInvalid,
                $"Environment metadata at '{environmentFilePath}' is invalid and was skipped.");
        }

        return new ChildReadFailure(
            PersistenceDiagnosticCodes.EnvironmentMetadataUnreadable,
            $"Environment metadata at '{environmentFilePath}' could not be read and was skipped.");
    }

    private static PersistenceDiagnostic CreateDiagnostic(
        string code,
        string sourcePath,
        string message)
    {
        return new PersistenceDiagnostic(
            code,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.Environment,
            sourcePath,
            message);
    }

    private sealed record EnvironmentLoadAttempt(
        DOPEnvironment? Environment,
        PersistenceDiagnostic? Diagnostic);

    private sealed record ChildReadFailure(
        string Code,
        string Message);
}
