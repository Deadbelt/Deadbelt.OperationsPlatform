using System.Text.Json;
using System.Text.Json.Serialization;
using Deadbelt.Application.Environments;
using Deadbelt.Domain.Environments;
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

    public async Task SaveAsync(
        DOPEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(environment.EnvironmentPath);

        var environmentFilePath = Path.Combine(
            environment.EnvironmentPath,
            EnvironmentFileName);

        if (File.Exists(environmentFilePath))
        {
            throw new InvalidOperationException(
                $"An environment already exists at '{environment.EnvironmentPath}'.");
        }

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

        await using var stream = File.Create(environmentFilePath);

        await JsonSerializer.SerializeAsync(
            stream,
            metadata,
            JsonOptions,
            cancellationToken);
    }

    public async Task<IReadOnlyList<DOPEnvironment>> LoadByWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        var environmentsRootPath = Path.Combine(
            workspacePath,
            EnvironmentsFolderName);

        if (!Directory.Exists(environmentsRootPath))
            return Array.Empty<DOPEnvironment>();

        var environments = new List<DOPEnvironment>();

        foreach (var environmentDirectory in Directory.EnumerateDirectories(environmentsRootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var environmentFilePath = Path.Combine(
                environmentDirectory,
                EnvironmentFileName);

            if (!File.Exists(environmentFilePath))
                continue;

            var environment = await TryLoadEnvironmentAsync(
                workspacePath,
                environmentFilePath,
                cancellationToken);

            if (environment is not null)
                environments.Add(environment);
        }

        return environments
            .OrderBy(environment => environment.Name)
            .ToArray();
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

    private static async Task<DOPEnvironment?> TryLoadEnvironmentAsync(
        string workspacePath,
        string environmentFilePath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(environmentFilePath);

            var metadata = await JsonSerializer.DeserializeAsync<EnvironmentMetadata>(
                stream,
                JsonOptions,
                cancellationToken);

            if (metadata is null)
                return null;

            return new DOPEnvironment(
                EnvironmentId.From(metadata.Id),
                workspacePath,
                metadata.Name,
                metadata.Description,
                metadata.GameType,
                metadata.EnvironmentPath,
                metadata.CreatedUtc,
                metadata.Version,
                metadata.Status);
        }
        catch
        {
            return null;
        }
    }
}
