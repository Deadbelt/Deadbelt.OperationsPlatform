using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Deadbelt.Application.Providers;
using Deadbelt.Domain.Providers;
using Microsoft.Extensions.Logging;

namespace Deadbelt.Infrastructure.Providers;

public sealed class JsonProviderStore : IProviderStore
{
    private const string ProvidersFolderName = "providers";
    private const string ProviderMetadataFileName = "provider.json";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly ILogger<JsonProviderStore> _logger;

    public JsonProviderStore(ILogger<JsonProviderStore> logger)
    {
        _logger = logger;
    }

    public string GetProviderPath(
        string workspacePath,
        string providerName)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            throw new ArgumentException("Workspace path is required.", nameof(workspacePath));

        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name is required.", nameof(providerName));

        return Path.Combine(
            workspacePath.Trim(),
            ProvidersFolderName,
            ToSafeFolderName(providerName));
    }

    public Task<bool> ExistsAsync(
        string workspacePath,
        string providerName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var providerPath = GetProviderPath(
            workspacePath,
            providerName);

        var metadataPath = Path.Combine(
            providerPath,
            ProviderMetadataFileName);

        return Task.FromResult(File.Exists(metadataPath));
    }

    public async Task SaveAsync(
        Provider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        Directory.CreateDirectory(provider.ProviderPath);

        var metadataPath = Path.Combine(
            provider.ProviderPath,
            ProviderMetadataFileName);

        if (File.Exists(metadataPath))
            throw new InvalidOperationException("Provider metadata already exists.");

        var metadata = ProviderMetadata.FromProvider(provider);

        await using var fileStream = File.Create(metadataPath);

        await JsonSerializer.SerializeAsync(
            fileStream,
            metadata,
            JsonOptions,
            cancellationToken);

        _logger.LogInformation(
            "Saved provider metadata to {ProviderMetadataPath}",
            metadataPath);
    }

    public async Task UpdateAsync(
        Provider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var metadataPath = Path.Combine(
            provider.ProviderPath,
            ProviderMetadataFileName);

        if (!File.Exists(metadataPath))
            throw new InvalidOperationException("Provider metadata does not exist.");

        var metadata = ProviderMetadata.FromProvider(provider);

        await using var fileStream = File.Create(metadataPath);

        await JsonSerializer.SerializeAsync(
            fileStream,
            metadata,
            JsonOptions,
            cancellationToken);

        _logger.LogInformation(
            "Updated provider metadata at {ProviderMetadataPath}",
            metadataPath);
    }

    public async Task<IReadOnlyList<Provider>> LoadByWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            throw new ArgumentException("Workspace path is required.", nameof(workspacePath));

        cancellationToken.ThrowIfCancellationRequested();

        var providersPath = Path.Combine(
            workspacePath.Trim(),
            ProvidersFolderName);

        if (!Directory.Exists(providersPath))
            return Array.Empty<Provider>();

        var providers = new List<Provider>();

        foreach (var providerDirectory in Directory.EnumerateDirectories(providersPath).OrderBy(path => path))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadataPath = Path.Combine(
                providerDirectory,
                ProviderMetadataFileName);

            if (!File.Exists(metadataPath))
            {
                _logger.LogDebug(
                    "Skipping provider folder without metadata file: {ProviderDirectory}",
                    providerDirectory);

                continue;
            }

            try
            {
                await using var fileStream = File.OpenRead(metadataPath);

                var metadata = await JsonSerializer.DeserializeAsync<ProviderMetadata>(
                    fileStream,
                    JsonOptions,
                    cancellationToken);

                if (metadata is null)
                {
                    _logger.LogWarning(
                        "Skipping provider metadata because it could not be deserialized: {ProviderMetadataPath}",
                        metadataPath);

                    continue;
                }

                providers.Add(metadata.ToProvider());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Skipping invalid provider metadata at {ProviderMetadataPath}",
                    metadataPath);
            }
        }

        return providers
            .OrderBy(provider => provider.Name)
            .ToArray();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }

    private static string ToSafeFolderName(string value)
    {
        var normalizedValue = value
            .Trim()
            .ToLowerInvariant();

        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in normalizedValue)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (char.IsWhiteSpace(character) || character == '-' || character == '_')
            {
                if (!previousWasSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                    previousWasSeparator = true;
                }

                continue;
            }
        }

        var safeFolderName = builder
            .ToString()
            .Trim('-');

        return string.IsNullOrWhiteSpace(safeFolderName)
            ? "provider"
            : safeFolderName;
    }
}
