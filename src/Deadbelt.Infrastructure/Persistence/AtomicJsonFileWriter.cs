using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Deadbelt.Infrastructure.Persistence;

internal static class AtomicJsonFileWriter
{
    private const string TemporaryFileSuffix = ".deadbelt.tmp";

    private static readonly IAtomicFileOperations FileOperations =
        new OperatingSystemAtomicFileOperations();

    public static Task WriteAsync<T>(
        string destinationPath,
        T document,
        JsonSerializerOptions serializerOptions,
        bool overwrite,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(
            destinationPath,
            document,
            serializerOptions,
            overwrite,
            logger,
            FileOperations,
            cancellationToken);
    }

    internal static async Task WriteAsync<T>(
        string destinationPath,
        T document,
        JsonSerializerOptions serializerOptions,
        bool overwrite,
        ILogger logger,
        IAtomicFileOperations fileOperations,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(serializerOptions);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(fileOperations);

        var destinationDirectory = Path.GetDirectoryName(destinationPath);

        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new InvalidOperationException(
                "Unable to determine the destination directory.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var temporaryFilePath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}{TemporaryFileSuffix}");

        var commitStarted = false;

        try
        {
            await using (var stream = fileOperations.CreateTemporaryFile(temporaryFilePath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document,
                    serializerOptions,
                    cancellationToken);

                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            commitStarted = true;

            if (overwrite)
            {
                fileOperations.ReplaceFile(
                    temporaryFilePath,
                    destinationPath);
            }
            else
            {
                fileOperations.MoveNewFile(
                    temporaryFilePath,
                    destinationPath);
            }
        }
        catch (OperationCanceledException) when (!commitStarted)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                commitStarted
                    ? "Atomic JSON commit failed for {DestinationPath}."
                    : "Atomic JSON write failed before commit for {DestinationPath}.",
                destinationPath);

            throw;
        }
        finally
        {
            try
            {
                if (fileOperations.FileExists(temporaryFilePath))
                    fileOperations.DeleteFile(temporaryFilePath);
            }
            catch (Exception cleanupException)
            {
                logger.LogWarning(
                    cleanupException,
                    "Failed to clean up temporary JSON file {TemporaryFilePath}.",
                    temporaryFilePath);
            }
        }
    }

}
