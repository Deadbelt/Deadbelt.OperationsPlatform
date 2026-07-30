using Deadbelt.Domain.Environments;

namespace Deadbelt.Desktop.ViewModels;

public sealed class DoctorEnvironmentOptionViewModel
{
    public DoctorEnvironmentOptionViewModel(
        EnvironmentId id,
        string name,
        GameType gameType)
    {
        Id = id;
        Name = name;
        GameType = gameType;
    }

    public EnvironmentId Id { get; }

    public string Name { get; }

    public GameType GameType { get; }

    public string DisplayName => $"{Name} ({GameType})";
}
