using Deadbelt.Domain.Environments;
using DOPEnvironment = Deadbelt.Domain.Environments.Environment;

namespace Deadbelt.Domain.Tests;

public sealed class EnvironmentTests
{
    [Fact]
    public void ConstructorPreservesValidValuesAndTrimsText()
    {
        var id = EnvironmentId.New();
        var createdUtc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        var environment = new DOPEnvironment(
            id,
            "  C:\\workspace  ",
            "  Production  ",
            "  Primary server  ",
            GameType.DayZ,
            "  C:\\workspace\\environments\\production  ",
            createdUtc,
            "  0.1  ",
            EnvironmentStatus.Active);

        Assert.Equal(id, environment.Id);
        Assert.Equal("C:\\workspace", environment.WorkspacePath);
        Assert.Equal("Production", environment.Name);
        Assert.Equal("Primary server", environment.Description);
        Assert.Equal(GameType.DayZ, environment.GameType);
        Assert.Equal("C:\\workspace\\environments\\production", environment.EnvironmentPath);
        Assert.Equal(createdUtc, environment.CreatedUtc);
        Assert.Equal("0.1", environment.Version);
        Assert.Equal(EnvironmentStatus.Active, environment.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorRejectsMissingWorkspacePath(string? workspacePath)
    {
        Assert.Throws<ArgumentException>(() => Create(workspacePath: workspacePath!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorRejectsMissingName(string? name)
    {
        Assert.Throws<ArgumentException>(() => Create(name: name!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorRejectsMissingEnvironmentPath(string? environmentPath)
    {
        Assert.Throws<ArgumentException>(() => Create(environmentPath: environmentPath!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorRejectsMissingVersion(string? version)
    {
        Assert.Throws<ArgumentException>(() => Create(version: version!));
    }

    [Fact]
    public void ConstructorRejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => new DOPEnvironment(
                default,
                "C:\\workspace",
                "Production",
                null,
                GameType.DayZ,
                "C:\\workspace\\environments\\production",
                DateTime.UtcNow,
                "0.1"));
    }

    [Fact]
    public void ConstructorRejectsUndefinedGameType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Create(gameType: (GameType)int.MaxValue));
    }

    [Fact]
    public void ConstructorRejectsUndefinedStatus()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Create(status: (EnvironmentStatus)int.MaxValue));
    }

    [Fact]
    public void ConstructorConvertsNullDescriptionToEmptyString()
    {
        var environment = Create();

        Assert.Equal(string.Empty, environment.Description);
    }

    private static DOPEnvironment Create(
        EnvironmentId? id = null,
        string workspacePath = "C:\\workspace",
        string name = "Production",
        GameType gameType = GameType.DayZ,
        string environmentPath = "C:\\workspace\\environments\\production",
        string version = "0.1",
        EnvironmentStatus status = EnvironmentStatus.Draft)
    {
        return new DOPEnvironment(
            id ?? EnvironmentId.New(),
            workspacePath,
            name,
            null,
            gameType,
            environmentPath,
            DateTime.UtcNow,
            version,
            status);
    }
}
