using System.Text.Json;
using System.Text.Json.Serialization;
using Deadbelt.Application.Environments;
using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Infrastructure.Environments;

public sealed class JsonEnvironmentStore : IEnvironmentStore
{
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
}