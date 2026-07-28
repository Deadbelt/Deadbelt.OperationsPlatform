namespace Deadbelt.Application.Tests.TestSupport;

internal sealed class TemporaryDirectory : IDisposable
{
    private const int MaxDeleteAttempts = 3;

    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "Deadbelt.Application.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string GetPath(params string[] segments)
    {
        return segments.Aggregate(Path, System.IO.Path.Combine);
    }

    public void Dispose()
    {
        for (var attempt = 1; attempt <= MaxDeleteAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);

                return;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException
                && attempt < MaxDeleteAttempts)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(10 * attempt));
            }
        }
    }
}
