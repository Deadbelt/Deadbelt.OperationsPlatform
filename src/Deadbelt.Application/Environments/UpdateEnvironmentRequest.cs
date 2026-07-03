using Deadbelt.Domain.Environments;

namespace Deadbelt.Application.Environments;

public sealed class UpdateEnvironmentRequest
{
    public required string WorkspacePath { get; init; }

    public required Guid EnvironmentId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required GameType GameType { get; init; }
}