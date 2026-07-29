using Deadbelt.Application.Persistence;

namespace Deadbelt.Infrastructure.Tests.TestSupport;

internal static class PersistenceDiagnosticAssertions
{
    public static PersistenceDiagnostic Single(
        IReadOnlyList<PersistenceDiagnostic> diagnostics,
        string expectedCode,
        PersistenceDiagnosticSeverity expectedSeverity,
        PersistenceResourceCategory expectedResourceCategory,
        string expectedSourcePath,
        string expectedMessageFragment,
        string? forbiddenExceptionMessage = null)
    {
        var diagnostic = Assert.Single(diagnostics);

        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal(expectedSeverity, diagnostic.Severity);
        Assert.Equal(expectedResourceCategory, diagnostic.ResourceCategory);
        Assert.Equal(expectedSourcePath, diagnostic.SourcePath);
        Assert.Contains(
            expectedMessageFragment,
            diagnostic.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            " at System.",
            diagnostic.Message,
            StringComparison.Ordinal);

        if (forbiddenExceptionMessage is not null)
        {
            Assert.DoesNotContain(
                forbiddenExceptionMessage,
                diagnostic.Message,
                StringComparison.Ordinal);
        }

        return diagnostic;
    }
}
