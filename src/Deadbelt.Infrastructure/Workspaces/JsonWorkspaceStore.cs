using System.Text.Json;
using Deadbelt.Application.Persistence;
using Deadbelt.Application.Workspaces;
using Deadbelt.Domain.Workspaces;
using Deadbelt.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Infrastructure.Workspaces;

public sealed class JsonWorkspaceStore : IWorkspaceStore
{
    private const string WorkspaceFileName = "workspace.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<JsonWorkspaceStore> _logger;
    private readonly IPersistenceReadOperations _readOperations;

    public JsonWorkspaceStore()
        : this(
            NullLogger<JsonWorkspaceStore>.Instance,
            new OperatingSystemPersistenceReadOperations())
    {
    }

    public JsonWorkspaceStore(ILogger<JsonWorkspaceStore> logger)
        : this(
            logger,
            new OperatingSystemPersistenceReadOperations())
    {
    }

    internal JsonWorkspaceStore(
        ILogger<JsonWorkspaceStore> logger,
        IPersistenceReadOperations readOperations)
    {
        _logger = logger;
        _readOperations = readOperations;
    }

    public async Task SaveAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default)
    {
        var workspaceFilePath = Path.Combine(
            workspace.Path,
            WorkspaceFileName);

        try
        {
            Directory.CreateDirectory(workspace.Path);

            if (File.Exists(workspaceFilePath))
            {
                throw new InvalidOperationException(
                    $"A workspace already exists at '{workspace.Path}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to prepare workspace metadata write at {WorkspaceFilePath}.",
                workspaceFilePath);

            throw;
        }

        var metadata = new WorkspaceMetadata
        {
            Name = workspace.Name,
            Description = workspace.Description,
            CreatedUtc = workspace.CreatedUtc,
            Version = workspace.Version
        };

        await AtomicJsonFileWriter.WriteAsync(
            workspaceFilePath,
            metadata,
            JsonOptions,
            overwrite: false,
            _logger,
            cancellationToken);
    }

    public async Task<PersistenceLoadResult<Workspace?>> LoadAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        var workspaceFilePath = Path.Combine(
            folderPath,
            WorkspaceFileName);
        var streamOpened = false;

        try
        {
            await using var stream = _readOperations.OpenRead(workspaceFilePath);
            streamOpened = true;

            var metadata = await JsonSerializer.DeserializeAsync<WorkspaceMetadata>(
                stream,
                JsonOptions,
                cancellationToken);

            if (metadata is null)
                throw new InvalidDataException("Workspace metadata deserialized to null.");

            if (string.IsNullOrWhiteSpace(metadata.Name))
                throw new InvalidDataException("Workspace metadata is missing a workspace name.");

            if (string.IsNullOrWhiteSpace(metadata.Version))
                throw new InvalidDataException("Workspace metadata is missing a workspace version.");

            var workspace = new Workspace(
                metadata.Name,
                folderPath,
                metadata.Description,
                metadata.CreatedUtc,
                metadata.Version);

            return PersistenceLoadResult<Workspace?>.Success(workspace);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var classification = ClassifyReadFailure(
                ex,
                workspaceFilePath,
                streamOpened);

            _logger.Log(
                classification.IsMissing
                    ? LogLevel.Warning
                    : LogLevel.Error,
                ex,
                "Workspace load blocked by {DiagnosticCode} at {WorkspaceFilePath}.",
                classification.Code,
                workspaceFilePath);

            return PersistenceLoadResult<Workspace?>.BlockingFailure(
                [
                    CreateDiagnostic(
                        classification.Code,
                        workspaceFilePath,
                        classification.Message)
                ]);
        }
    }

    private static WorkspaceReadFailure ClassifyReadFailure(
        Exception exception,
        string workspaceFilePath,
        bool streamOpened)
    {
        if (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new WorkspaceReadFailure(
                PersistenceDiagnosticCodes.WorkspaceMetadataMissing,
                $"Required workspace metadata was not found at '{workspaceFilePath}'.",
                true);
        }

        if (streamOpened
            && exception is JsonException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException)
        {
            return new WorkspaceReadFailure(
                PersistenceDiagnosticCodes.WorkspaceMetadataInvalid,
                $"Workspace metadata at '{workspaceFilePath}' is invalid.",
                false);
        }

        return new WorkspaceReadFailure(
            PersistenceDiagnosticCodes.WorkspaceMetadataUnreadable,
            $"Workspace metadata at '{workspaceFilePath}' could not be read.",
            false);
    }

    private static PersistenceDiagnostic CreateDiagnostic(
        string code,
        string sourcePath,
        string message)
    {
        return new PersistenceDiagnostic(
            code,
            PersistenceDiagnosticSeverity.Error,
            PersistenceResourceCategory.Workspace,
            sourcePath,
            message);
    }

    private sealed record WorkspaceReadFailure(
        string Code,
        string Message,
        bool IsMissing);
}
