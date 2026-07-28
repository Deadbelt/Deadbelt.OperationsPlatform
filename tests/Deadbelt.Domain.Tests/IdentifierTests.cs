using Deadbelt.Domain.Environments;
using Deadbelt.Domain.Providers;

namespace Deadbelt.Domain.Tests;

public sealed class IdentifierTests
{
    [Fact]
    public void EnvironmentIdNewCreatesNonEmptyIdentifier()
    {
        Assert.NotEqual(Guid.Empty, EnvironmentId.New().Value);
    }

    [Fact]
    public void EnvironmentIdFromRejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(() => EnvironmentId.From(Guid.Empty));
    }

    [Fact]
    public void ProviderIdNewCreatesNonEmptyIdentifier()
    {
        Assert.NotEqual(Guid.Empty, ProviderId.New().Value);
    }

    [Fact]
    public void ProviderIdFromRejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(() => ProviderId.From(Guid.Empty));
    }

    [Fact]
    public void ProviderIdPrimaryConstructorCurrentlyAllowsEmptyIdentifier()
    {
        var id = new ProviderId(Guid.Empty);

        Assert.Equal(Guid.Empty, id.Value);
    }
}
