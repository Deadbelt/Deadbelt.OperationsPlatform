using Deadbelt.Domain.Environments;

namespace Deadbelt.Application.Environments;

public sealed class CreateEnvironmentRequest
{
    public required string WorkspacePath { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required GameType GameType { get; init; }
}