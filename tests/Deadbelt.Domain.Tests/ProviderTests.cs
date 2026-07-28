using Deadbelt.Domain.Providers;

namespace Deadbelt.Domain.Tests;

public sealed class ProviderTests
{
    [Fact]
    public void ConstructorPreservesValidValuesAndNormalizesUtc()
    {
        var id = ProviderId.New();
        var localTime = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Local);

        var provider = new Provider(
            id,
            "  C:\\workspace  ",
            "  Local Host  ",
            ProviderType.LocalWindows,
            "  C:\\workspace\\providers\\local-host  ",
            ProviderStatus.Configured,
            localTime,
            "  0.1  ");

        Assert.Equal(id, provider.Id);
        Assert.Equal("C:\\workspace", provider.WorkspacePath);
        Assert.Equal("Local Host", provider.Name);
        Assert.Equal(ProviderType.LocalWindows, provider.ProviderType);
        Assert.Equal("C:\\workspace\\providers\\local-host", provider.ProviderPath);
        Assert.Equal(ProviderStatus.Configured, provider.Status);
        Assert.Equal(DateTimeKind.Utc, provider.CreatedUtc.Kind);
        Assert.Equal(localTime.ToUniversalTime(), provider.CreatedUtc);
        Assert.Equal("0.1", provider.Version);
    }

    [Fact]
    public void CreateUsesDraftStatusAndCurrentVersion()
    {
        var provider = Provider.Create(
            "C:\\workspace",
            "Local Host",
            ProviderType.LocalWindows,
            "C:\\workspace\\providers\\local-host");

        Assert.NotEqual(Guid.Empty, provider.Id.Value);
        Assert.Equal(ProviderStatus.Draft, provider.Status);
        Assert.Equal(Provider.CurrentVersion, provider.Version);
        Assert.Equal(DateTimeKind.Utc, provider.CreatedUtc.Kind);
    }

    [Fact]
    public void ConstructorRejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => new Provider(
                default,
                "C:\\workspace",
                "Local Host",
                ProviderType.LocalWindows,
                "C:\\workspace\\providers\\local-host",
                ProviderStatus.Draft,
                DateTime.UtcNow,
                "0.1"));
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

    [Fact]
    public void ConstructorRejectsUnknownProviderType()
    {
        Assert.Throws<ArgumentException>(() => Create(providerType: ProviderType.Unknown));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorRejectsMissingProviderPath(string? providerPath)
    {
        Assert.Throws<ArgumentException>(() => Create(providerPath: providerPath!));
    }

    [Fact]
    public void ConstructorRejectsUnknownStatus()
    {
        Assert.Throws<ArgumentException>(() => Create(status: ProviderStatus.Unknown));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConstructorUsesCurrentVersionWhenVersionIsMissing(string? version)
    {
        var provider = Create(version: version);

        Assert.Equal(Provider.CurrentVersion, provider.Version);
    }

    [Fact]
    public void ConstructorCurrentlyAcceptsUndefinedNonzeroProviderType()
    {
        var undefinedProviderType = (ProviderType)12345;

        var provider = Create(providerType: undefinedProviderType);

        Assert.Equal(undefinedProviderType, provider.ProviderType);
    }

    [Fact]
    public void ConstructorCurrentlyAcceptsUndefinedNonzeroProviderStatus()
    {
        var undefinedProviderStatus = (ProviderStatus)12345;

        var provider = Create(status: undefinedProviderStatus);

        Assert.Equal(undefinedProviderStatus, provider.Status);
    }

    private static Provider Create(
        ProviderId? id = null,
        string workspacePath = "C:\\workspace",
        string name = "Local Host",
        ProviderType providerType = ProviderType.LocalWindows,
        string providerPath = "C:\\workspace\\providers\\local-host",
        ProviderStatus status = ProviderStatus.Draft,
        string? version = "0.1")
    {
        return new Provider(
            id ?? ProviderId.New(),
            workspacePath,
            name,
            providerType,
            providerPath,
            status,
            DateTime.UtcNow,
            version!);
    }
}
