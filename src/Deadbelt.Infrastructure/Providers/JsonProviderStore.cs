using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Deadbelt.Application.Persistence;
using Deadbelt.Application.Providers;
using Deadbelt.Domain.Providers;
using Deadbelt.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Deadbelt.Infrastructure.Providers;

public sealed class JsonProviderStore : IProviderStore
{
    private const string ProvidersFolderName = "providers";
    private const string ProviderMetadataFileName = "provider.json";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly ILogger<JsonProviderStore> _logger;
    private readonly IPersistenceReadOperations _readOperations;

    public JsonProviderStore(ILogger<JsonProviderStore> logger)
        : this(
            logger,
            new OperatingSystemPersistenceReadOperations())
    {
    }

    internal JsonProviderStore(
        ILogger<JsonProviderStore> logger,
        IPersistenceReadOperations readOperations)
    {
        _logger = logger;
        _readOperations = readOperations;
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

        var metadataPath = Path.Combine(
            provider.ProviderPath,
            ProviderMetadataFileName);

        try
        {
            Directory.CreateDirectory(provider.ProviderPath);

            if (File.Exists(metadataPath))
                throw new InvalidOperationException("Provider metadata already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to prepare provider metadata write at {ProviderMetadataPath}.",
                metadataPath);

            throw;
        }

        await WriteMetadataAsync(
            provider,
            metadataPath,
            overwrite: false,
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
        {
            var exception = new InvalidOperationException(
                "Provider metadata does not exist.");

            _logger.LogError(
                exception,
                "Failed to prepare provider metadata update at {ProviderMetadataPath}.",
                metadataPath);

            throw exception;
        }

        await WriteMetadataAsync(
            provider,
            metadataPath,
            overwrite: true,
            cancellationToken);

        _logger.LogInformation(
            "Updated provider metadata at {ProviderMetadataPath}",
            metadataPath);
    }

    public async Task<PersistenceLoadResult<IReadOnlyList<Provider>>> LoadByWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            throw new ArgumentException("Workspace path is required.", nameof(workspacePath));

        cancellationToken.ThrowIfCancellationRequested();

        var providersPath = Path.Combine(
            workspacePath.Trim(),
            ProvidersFolderName);

        IReadOnlyList<string> providerDirectories;

        try
        {
            providerDirectories = _readOperations
                .EnumerateDirectories(providersPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return PersistenceLoadResult<IReadOnlyList<Provider>>.Success(
                Array.Empty<Provider>());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var diagnostic = CreateDiagnostic(
                PersistenceDiagnosticCodes.ProviderCollectionUnreadable,
                providersPath,
                $"Provider metadata could not be enumerated under '{providersPath}'.");

            _logger.LogError(
                ex,
                "Failed to enumerate provider metadata under {ProvidersPath}.",
                providersPath);

            return PersistenceLoadResult<IReadOnlyList<Provider>>.Success(
                Array.Empty<Provider>(),
                [diagnostic]);
        }

        var providers = new List<Provider>();
        var diagnostics = new List<PersistenceDiagnostic>();

        foreach (var providerDirectory in providerDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadataPath = Path.Combine(
                providerDirectory,
                ProviderMetadataFileName);

            var loadResult = await LoadProviderAsync(
                metadataPath,
                cancellationToken);

            if (loadResult.Provider is not null)
                providers.Add(loadResult.Provider);

            if (loadResult.Diagnostic is not null)
                diagnostics.Add(loadResult.Diagnostic);
        }

        return PersistenceLoadResult<IReadOnlyList<Provider>>.Success(
            providers
                .OrderBy(provider => provider.Name)
                .ToArray(),
            diagnostics);
    }

    private async Task WriteMetadataAsync(
        Provider provider,
        string metadataPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var metadata = ProviderMetadata.FromProvider(provider);

        await AtomicJsonFileWriter.WriteAsync(
            metadataPath,
            metadata,
            JsonOptions,
            overwrite,
            _logger,
            cancellationToken);
    }

    private async Task<ProviderLoadAttempt> LoadProviderAsync(
        string metadataPath,
        CancellationToken cancellationToken)
    {
        var streamOpened = false;

        try
        {
            await using var fileStream = _readOperations.OpenRead(metadataPath);
            streamOpened = true;

            var metadata = await JsonSerializer.DeserializeAsync<ProviderMetadata>(
                fileStream,
                JsonOptions,
                cancellationToken);

            if (metadata is null)
                throw new InvalidDataException("Provider metadata deserialized to null.");

            return new ProviderLoadAttempt(
                metadata.ToProvider(),
                null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var classification = ClassifyChildReadFailure(
                ex,
                metadataPath,
                streamOpened);
            var diagnostic = CreateDiagnostic(
                classification.Code,
                metadataPath,
                classification.Message);

            _logger.LogWarning(
                ex,
                "Skipping provider metadata with {DiagnosticCode} at {ProviderMetadataPath}.",
                classification.Code,
                metadataPath);

            return new ProviderLoadAttempt(
                null,
                diagnostic);
        }
    }

    private static ChildReadFailure ClassifyChildReadFailure(
        Exception exception,
        string metadataPath,
        bool streamOpened)
    {
        if (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new ChildReadFailure(
                PersistenceDiagnosticCodes.ProviderMetadataMissing,
                $"Provider metadata was not found at '{metadataPath}'.");
        }

        if (streamOpened
            && exception is JsonException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException)
        {
            return new ChildReadFailure(
                PersistenceDiagnosticCodes.ProviderMetadataInvalid,
                $"Provider metadata at '{metadataPath}' is invalid and was skipped.");
        }

        return new ChildReadFailure(
            PersistenceDiagnosticCodes.ProviderMetadataUnreadable,
            $"Provider metadata at '{metadataPath}' could not be read and was skipped.");
    }

    private static PersistenceDiagnostic CreateDiagnostic(
        string code,
        string sourcePath,
        string message)
    {
        return new PersistenceDiagnostic(
            code,
            PersistenceDiagnosticSeverity.Warning,
            PersistenceResourceCategory.Provider,
            sourcePath,
            message);
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

    private sealed record ProviderLoadAttempt(
        Provider? Provider,
        PersistenceDiagnostic? Diagnostic);

    private sealed record ChildReadFailure(
        string Code,
        string Message);
}
