using Deadbelt.Application.Common;
using Deadbelt.Application.Persistence;
using Deadbelt.Domain.Workspaces;
using Microsoft.Extensions.Logging;

namespace Deadbelt.Application.Workspaces;

public sealed class WorkspaceService : IWorkspaceService
{
    private const string CurrentWorkspaceVersion = "0.1";

    private readonly IWorkspaceStore _workspaceStore;
    private readonly IPathInspector _pathInspector;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(
        IWorkspaceStore workspaceStore,
        IPathInspector pathInspector,
        ILogger<WorkspaceService> logger)
    {
        _workspaceStore = workspaceStore;
        _pathInspector = pathInspector;
        _logger = logger;
    }

    public async Task<CreateWorkspaceResult> CreateWorkspaceAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return CreateWorkspaceResult.Failure("Workspace name is required.");

        if (string.IsNullOrWhiteSpace(request.FolderPath))
            return CreateWorkspaceResult.Failure("Workspace folder is required.");

        if (!PathInspection.IsValidFullyQualifiedFolderPath(
                _pathInspector,
                request.FolderPath))
            return CreateWorkspaceResult.Failure("Workspace folder must be a valid full path.");

        try
        {
            var workspace = new Workspace(
                request.Name,
                request.FolderPath,
                request.Description,
                DateTime.UtcNow,
                CurrentWorkspaceVersion);

            await _workspaceStore.SaveAsync(workspace, cancellationToken);

            _logger.LogInformation(
                "Workspace created: {WorkspaceName} at {WorkspacePath}",
                workspace.Name,
                workspace.Path);

            return CreateWorkspaceResult.Success(workspace);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Workspace creation validation failed.");

            return CreateWorkspaceResult.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workspace.");

            return CreateWorkspaceResult.Failure(
                "Failed to create workspace. See logs for details.");
        }
    }

    public async Task<OpenWorkspaceResult> OpenWorkspaceAsync(
        OpenWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FolderPath))
            return OpenWorkspaceResult.Failure("Workspace folder is required.");

        if (!PathInspection.IsValidFullyQualifiedFolderPath(
                _pathInspector,
                request.FolderPath))
            return OpenWorkspaceResult.Failure("Workspace folder must be a valid full path.");

        try
        {
            var loadResult = await _workspaceStore.LoadAsync(
                request.FolderPath,
                cancellationToken);

            var workspace = loadResult.Value;

            if (workspace is null)
            {
                var diagnostic = loadResult.Diagnostics.FirstOrDefault();
                var errorMessage = diagnostic?.Message
                    ?? "The selected folder is not a valid Deadbelt workspace.";

                _logger.LogWarning(
                    "Workspace open blocked by {DiagnosticCode} at {SourcePath}.",
                    diagnostic?.Code,
                    diagnostic?.SourcePath);

                return OpenWorkspaceResult.BlockingFailure(
                    errorMessage,
                    loadResult.Diagnostics);
            }

            _logger.LogInformation(
                "Workspace opened: {WorkspaceName} at {WorkspacePath}",
                workspace.Name,
                workspace.Path);

            return OpenWorkspaceResult.Success(
                workspace,
                loadResult.Diagnostics);
        }
        catch (Exception ex)
        {
            var workspaceFilePath = Path.Combine(
                request.FolderPath,
                "workspace.json");
            var diagnostic = new PersistenceDiagnostic(
                PersistenceDiagnosticCodes.WorkspaceMetadataUnreadable,
                PersistenceDiagnosticSeverity.Error,
                PersistenceResourceCategory.Workspace,
                workspaceFilePath,
                $"Workspace metadata at '{workspaceFilePath}' could not be read.");

            _logger.LogError(
                ex,
                "Workspace open blocked by an unexpected load failure at {WorkspaceFilePath}.",
                workspaceFilePath);

            return OpenWorkspaceResult.BlockingFailure(
                diagnostic.Message,
                [diagnostic]);
        }
    }
}
