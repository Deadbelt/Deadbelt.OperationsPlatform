using Deadbelt.Application.Persistence;
using Deadbelt.Domain.Workspaces;
using Microsoft.Extensions.Logging;

namespace Deadbelt.Application.Workspaces;

public sealed class RecentWorkspaceService : IRecentWorkspaceService
{
    private const int MaxRecentWorkspaces = 10;

    private readonly IRecentWorkspaceStore _recentWorkspaceStore;
    private readonly ILogger<RecentWorkspaceService> _logger;

    public RecentWorkspaceService(
        IRecentWorkspaceStore recentWorkspaceStore,
        ILogger<RecentWorkspaceService> logger)
    {
        _recentWorkspaceStore = recentWorkspaceStore;
        _logger = logger;
    }

    public async Task<PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>> GetRecentWorkspacesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var loadResult = await _recentWorkspaceStore.LoadAsync(
                cancellationToken);

            var recentWorkspaces = loadResult.Value
                .OrderByDescending(workspace => workspace.LastOpenedUtc)
                .Take(MaxRecentWorkspaces)
                .ToArray();

            if (loadResult.HasDiagnostics)
            {
                _logger.LogWarning(
                    "Recent workspace loading completed with {DiagnosticCount} diagnostic(s).",
                    loadResult.Diagnostics.Count);
            }

            return PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>.Success(
                recentWorkspaces,
                loadResult.Diagnostics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recent workspaces.");

            var diagnostic = new PersistenceDiagnostic(
                PersistenceDiagnosticCodes.RecentWorkspaceSettingsUnreadable,
                PersistenceDiagnosticSeverity.Warning,
                PersistenceResourceCategory.RecentWorkspaces,
                PersistenceDiagnostic.UnknownSourcePath,
                "Recent workspace settings could not be loaded.");

            return PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>.Success(
                Array.Empty<RecentWorkspace>(),
                [diagnostic]);
        }
    }

    public async Task RecordWorkspaceAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        try
        {
            var loadResult = await _recentWorkspaceStore.LoadAsync(
                cancellationToken);
            var existingRecentWorkspaces = loadResult.Value;

            var recentWorkspace = new RecentWorkspace(
                workspace.Name,
                workspace.Path,
                DateTime.UtcNow);

            var updatedRecentWorkspaces = existingRecentWorkspaces
                .Where(existingWorkspace =>
                    !string.Equals(
                        existingWorkspace.Path,
                        workspace.Path,
                        StringComparison.OrdinalIgnoreCase))
                .Prepend(recentWorkspace)
                .OrderByDescending(existingWorkspace => existingWorkspace.LastOpenedUtc)
                .Take(MaxRecentWorkspaces)
                .ToArray();

            await _recentWorkspaceStore.SaveAsync(
                updatedRecentWorkspaces,
                cancellationToken);

            _logger.LogInformation(
                "Recorded recent workspace at {WorkspacePath}",
                workspace.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record recent workspace at {WorkspacePath}",
                workspace.Path);
        }
    }

    public async Task RemoveWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            _logger.LogWarning("Unable to remove recent workspace because workspace path is empty.");
            return;
        }

        try
        {
            var loadResult = await _recentWorkspaceStore.LoadAsync(
                cancellationToken);
            var existingRecentWorkspaces = loadResult.Value;

            var updatedRecentWorkspaces = existingRecentWorkspaces
                .Where(recentWorkspace =>
                    !string.Equals(
                        recentWorkspace.Path,
                        workspacePath,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(recentWorkspace => recentWorkspace.LastOpenedUtc)
                .Take(MaxRecentWorkspaces)
                .ToArray();

            await _recentWorkspaceStore.SaveAsync(
                updatedRecentWorkspaces,
                cancellationToken);

            _logger.LogInformation(
                "Removed recent workspace at {WorkspacePath}",
                workspacePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to remove recent workspace at {WorkspacePath}",
                workspacePath);
        }
    }
}
