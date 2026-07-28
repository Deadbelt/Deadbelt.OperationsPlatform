using Deadbelt.Domain.Workspaces;

namespace Deadbelt.Domain.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void WorkspaceIsCompiledIntoDomainAssembly()
    {
        var workspaceAssemblyName = typeof(Workspace).Assembly.GetName().Name;

        Assert.Equal("Deadbelt.Domain", workspaceAssemblyName);
        Assert.NotEqual("Deadbelt.Application", workspaceAssemblyName);
    }
}
