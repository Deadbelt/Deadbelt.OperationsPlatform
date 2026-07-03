using Deadbelt.Domain.Environments;

namespace Deadbelt.Desktop.Services;

public sealed class EditEnvironmentDialogResult
{
    private EditEnvironmentDialogResult(
        bool confirmed,
        string name,
        string? description,
        GameType gameType)
    {
        Confirmed = confirmed;
        Name = name;
        Description = description;
        GameType = gameType;
    }

    public bool Confirmed { get; }

    public string Name { get; }

    public string? Description { get; }

    public GameType GameType { get; }

    public static EditEnvironmentDialogResult Cancelled()
    {
        return new EditEnvironmentDialogResult(false, string.Empty, null, GameType.Unknown);
    }

    public static EditEnvironmentDialogResult Success(
        string name,
        string? description,
        GameType gameType)
    {
        return new EditEnvironmentDialogResult(true, name, description, gameType);
    }
}