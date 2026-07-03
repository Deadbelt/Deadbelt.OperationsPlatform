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

    public async Task<IReadOnlyList<RecentWorkspace>> GetRecentWorkspacesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var recentWorkspaces = await _recentWorkspaceStore.LoadAsync(
                cancellationToken);

            return recentWorkspaces
                .OrderByDescending(workspace => workspace.LastOpenedUtc)
                .Take(MaxRecentWorkspaces)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recent workspaces.");

            return Array.Empty<RecentWorkspace>();
        }
    }

    public async Task RecordWorkspaceAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existingRecentWorkspaces = await _recentWorkspaceStore.LoadAsync(
                cancellationToken);

            var updatedRecentWorkspaces = existingRecentWorkspaces
                .Where(recentWorkspace =>
                    !string.Equals(
                        recentWorkspace.Path,
                        workspace.Path,
                        StringComparison.OrdinalIgnoreCase))
                .Prepend(
                    new RecentWorkspace(
                        workspace.Name,
                        workspace.Path,
                        DateTime.UtcNow))
                .OrderByDescending(recentWorkspace => recentWorkspace.LastOpenedUtc)
                .Take(MaxRecentWorkspaces)
                .ToArray();

            await _recentWorkspaceStore.SaveAsync(
                updatedRecentWorkspaces,
                cancellationToken);

            _logger.LogInformation(
                "Recorded recent workspace: {WorkspaceName} at {WorkspacePath}",
                workspace.Name,
                workspace.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record recent workspace: {WorkspaceName} at {WorkspacePath}",
                workspace.Name,
                workspace.Path);
        }
    }
}