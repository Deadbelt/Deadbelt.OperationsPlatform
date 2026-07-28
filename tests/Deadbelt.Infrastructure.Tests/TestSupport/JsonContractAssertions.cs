using System.Text.Json;

namespace Deadbelt.Infrastructure.Tests.TestSupport;

internal static class JsonContractAssertions
{
    public static void HasExactlyProperties(
        JsonElement element,
        params string[] expectedPropertyNames)
    {
        var actualPropertyNames = element
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var expectedNames = expectedPropertyNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedNames, actualPropertyNames);
    }
}
