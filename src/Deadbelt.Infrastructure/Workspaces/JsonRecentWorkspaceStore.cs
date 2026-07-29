using System.Text.Json;
using Deadbelt.Application.Persistence;
using Deadbelt.Application.Workspaces;
using Deadbelt.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Infrastructure.Workspaces;

public sealed class JsonRecentWorkspaceStore : IRecentWorkspaceStore
{
    private const string SettingsFolderName = "Deadbelt";
    private const string ProductFolderName = "OperationsPlatform";
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private readonly ILogger<JsonRecentWorkspaceStore> _logger;
    private readonly IPersistenceReadOperations _readOperations;

    public JsonRecentWorkspaceStore()
        : this(
            GetDefaultSettingsFilePath(),
            NullLogger<JsonRecentWorkspaceStore>.Instance,
            new OperatingSystemPersistenceReadOperations())
    {
    }

    public JsonRecentWorkspaceStore(ILogger<JsonRecentWorkspaceStore> logger)
        : this(
            GetDefaultSettingsFilePath(),
            logger,
            new OperatingSystemPersistenceReadOperations())
    {
    }

    internal JsonRecentWorkspaceStore(string settingsFilePath)
        : this(
            settingsFilePath,
            NullLogger<JsonRecentWorkspaceStore>.Instance,
            new OperatingSystemPersistenceReadOperations())
    {
    }

    internal JsonRecentWorkspaceStore(
        string settingsFilePath,
        ILogger<JsonRecentWorkspaceStore> logger)
        : this(
            settingsFilePath,
            logger,
            new OperatingSystemPersistenceReadOperations())
    {
    }

    internal JsonRecentWorkspaceStore(
        string settingsFilePath,
        ILogger<JsonRecentWorkspaceStore> logger,
        IPersistenceReadOperations readOperations)
    {
        if (string.IsNullOrWhiteSpace(settingsFilePath))
            throw new ArgumentException("Settings file path is required.", nameof(settingsFilePath));

        _settingsFilePath = settingsFilePath;
        _logger = logger;
        _readOperations = readOperations;
    }

    public async Task<PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var streamOpened = false;

        try
        {
            await using var stream = _readOperations.OpenRead(_settingsFilePath);
            streamOpened = true;

            var settings = await JsonSerializer.DeserializeAsync<RecentWorkspaceSettings>(
                stream,
                JsonOptions,
                cancellationToken);

            if (settings is null)
                throw new InvalidDataException("Recent workspace settings deserialized to null.");

            var invalidEntryFound = settings.RecentWorkspaces.Any(metadata =>
                string.IsNullOrWhiteSpace(metadata.Name)
                || string.IsNullOrWhiteSpace(metadata.Path));

            var recentWorkspaces = settings.RecentWorkspaces
                .Where(metadata =>
                    !string.IsNullOrWhiteSpace(metadata.Name)
                    && !string.IsNullOrWhiteSpace(metadata.Path))
                .Select(metadata =>
                    new RecentWorkspace(
                        metadata.Name,
                        metadata.Path,
                        metadata.LastOpenedUtc))
                .OrderByDescending(workspace => workspace.LastOpenedUtc)
                .ToArray();

            if (!invalidEntryFound)
            {
                return PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>.Success(
                    recentWorkspaces);
            }

            var diagnostic = CreateDiagnostic(
                PersistenceDiagnosticCodes.RecentWorkspaceSettingsInvalid,
                $"Recent workspace settings at '{_settingsFilePath}' contain invalid entries.");

            _logger.LogWarning(
                "Recent workspace settings contain invalid entries at {SettingsFilePath}.",
                _settingsFilePath);

            return PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>.Success(
                recentWorkspaces,
                [diagnostic]);
        }
        catch (Exception ex) when (
            ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>.Success(
                Array.Empty<RecentWorkspace>());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var isInvalid = streamOpened
                && ex is JsonException
                or InvalidDataException
                or ArgumentException
                or InvalidOperationException;
            var code = isInvalid
                ? PersistenceDiagnosticCodes.RecentWorkspaceSettingsInvalid
                : PersistenceDiagnosticCodes.RecentWorkspaceSettingsUnreadable;
            var message = isInvalid
                ? $"Recent workspace settings at '{_settingsFilePath}' are invalid."
                : $"Recent workspace settings at '{_settingsFilePath}' could not be read.";

            _logger.LogError(
                ex,
                "Failed to load recent workspace settings with {DiagnosticCode} at {SettingsFilePath}.",
                code,
                _settingsFilePath);

            return PersistenceLoadResult<IReadOnlyList<RecentWorkspace>>.Success(
                Array.Empty<RecentWorkspace>(),
                [CreateDiagnostic(code, message)]);
        }
    }

    public async Task SaveAsync(
        IReadOnlyList<RecentWorkspace> recentWorkspaces,
        CancellationToken cancellationToken = default)
    {
        var settingsFolderPath = Path.GetDirectoryName(_settingsFilePath);

        if (string.IsNullOrWhiteSpace(settingsFolderPath))
        {
            var exception = new InvalidOperationException(
                "Unable to determine settings folder path.");

            _logger.LogError(
                exception,
                "Failed to prepare recent workspace settings write at {SettingsFilePath}.",
                _settingsFilePath);

            throw exception;
        }

        try
        {
            Directory.CreateDirectory(settingsFolderPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to prepare recent workspace settings write at {SettingsFilePath}.",
                _settingsFilePath);

            throw;
        }

        var settings = new RecentWorkspaceSettings
        {
            RecentWorkspaces = recentWorkspaces
                .Select(workspace =>
                    new RecentWorkspaceMetadata
                    {
                        Name = workspace.Name,
                        Path = workspace.Path,
                        LastOpenedUtc = workspace.LastOpenedUtc
                    })
                .ToList()
        };

        await AtomicJsonFileWriter.WriteAsync(
            _settingsFilePath,
            settings,
            JsonOptions,
            overwrite: File.Exists(_settingsFilePath),
            _logger,
            cancellationToken);
    }

    private PersistenceDiagnostic CreateDiagnostic(
        string code,
        string message)
    {
        return new PersistenceDiagnostic(
            code,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.RecentWorkspaces,
            _settingsFilePath,
            message);
    }

    private static string GetDefaultSettingsFilePath()
    {
        var appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);

        return Path.Combine(
            appDataPath,
            SettingsFolderName,
            ProductFolderName,
            SettingsFileName);
    }
}
