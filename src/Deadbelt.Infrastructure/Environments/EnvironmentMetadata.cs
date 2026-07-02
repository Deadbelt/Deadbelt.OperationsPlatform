using Deadbelt.Domain.Environments;

namespace Deadbelt.Infrastructure.Environments;

internal sealed class EnvironmentMetadata
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required GameType GameType { get; init; }

    public required string EnvironmentPath { get; init; }

    public required DateTime CreatedUtc { get; init; }

    public required string Version { get; init; }

    public required EnvironmentStatus Status { get; init; }
}