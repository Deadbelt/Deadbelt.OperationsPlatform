using System.Text.Json;
using Deadbelt.Infrastructure.Persistence;
using Deadbelt.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadbelt.Infrastructure.Tests;

public sealed class AtomicJsonFileWriterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    [Fact]
    public async Task FirstWriteUsesNonOverwritingMoveAndLeavesOnlyDestination()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destinationPath = temporaryDirectory.GetPath("document.json");
        var fileOperations = new RecordingAtomicFileOperations();

        await AtomicJsonFileWriter.WriteAsync(
            destinationPath,
            new TestDocument("first"),
            JsonOptions,
            overwrite: false,
            NullLogger.Instance,
            fileOperations);

        Assert.Equal(1, fileOperations.MoveNewCallCount);
        Assert.Equal(0, fileOperations.ReplaceCallCount);
        AssertDocumentValue(destinationPath, "first");
        AssertOnlyExpectedEntries(temporaryDirectory.Path, "document.json");
    }

    [Fact]
    public async Task ExistingWriteUsesReplaceAndLeavesOnlyDestination()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destinationPath = temporaryDirectory.GetPath("document.json");
        await File.WriteAllBytesAsync(destinationPath, "original"u8.ToArray());
        var fileOperations = new RecordingAtomicFileOperations();

        await AtomicJsonFileWriter.WriteAsync(
            destinationPath,
            new TestDocument("replacement"),
            JsonOptions,
            overwrite: true,
            NullLogger.Instance,
            fileOperations);

        Assert.Equal(0, fileOperations.MoveNewCallCount);
        Assert.Equal(1, fileOperations.ReplaceCallCount);
        AssertDocumentValue(destinationPath, "replacement");
        AssertOnlyExpectedEntries(temporaryDirectory.Path, "document.json");
    }

    [Fact]
    public async Task ExistingWritePreservesCreationTimeOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var temporaryDirectory = new TemporaryDirectory();
        var destinationPath = temporaryDirectory.GetPath("document.json");
        await File.WriteAllBytesAsync(destinationPath, "original"u8.ToArray());
        var expectedCreationTime = new DateTime(
            2020,
            1,
            2,
            3,
            4,
            6,
            DateTimeKind.Utc);
        File.SetCreationTimeUtc(destinationPath, expectedCreationTime);

        await AtomicJsonFileWriter.WriteAsync(
            destinationPath,
            new TestDocument("replacement"),
            JsonOptions,
            overwrite: true,
            NullLogger.Instance);

        Assert.Equal(
            expectedCreationTime,
            File.GetCreationTimeUtc(destinationPath));
        AssertOnlyExpectedEntries(temporaryDirectory.Path, "document.json");
    }

    [Fact]
    public async Task SerializationFailurePreservesOriginalBytesCleansTemporaryFileAndLogsError()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destinationPath = temporaryDirectory.GetPath("document.json");
        var originalBytes = "original-destination-bytes"u8.ToArray();
        await File.WriteAllBytesAsync(destinationPath, originalBytes);
        var logger = new RecordingLogger();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => AtomicJsonFileWriter.WriteAsync(
                destinationPath,
                new UnsupportedDocument(
                    "replacement",
                    () => { }),
                JsonOptions,
                overwrite: true,
                logger));

        Assert.Equal(
            originalBytes,
            await File.ReadAllBytesAsync(destinationPath));
        var errorEntry = Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Error);
        Assert.Same(exception, errorEntry.Exception);
        Assert.Contains(
            "failed before commit",
            errorEntry.Message,
            StringComparison.OrdinalIgnoreCase);
        AssertOnlyExpectedEntries(temporaryDirectory.Path, "document.json");
    }

    [Fact]
    public async Task TemporaryWriteFailurePreservesOriginalBytesAndLogsError()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destinationPath = temporaryDirectory.GetPath("document.json");
        var originalBytes = "original-destination-bytes"u8.ToArray();
        await File.WriteAllBytesAsync(destinationPath, originalBytes);
        var logger = new RecordingLogger();
        var expectedException = new IOException("Deterministic temporary write failure.");

        var exception = await Assert.ThrowsAsync<IOException>(
            () => AtomicJsonFileWriter.WriteAsync(
                destinationPath,
                new TestDocument("replacement"),
                JsonOptions,
                overwrite: true,
                logger,
                new WriteFailureFileOperations(expectedException)));

        Assert.Same(expectedException, exception);
        Assert.Equal(
            originalBytes,
            await File.ReadAllBytesAsync(destinationPath));
        var errorEntry = Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Error);
        Assert.Same(expectedException, errorEntry.Exception);
        Assert.Contains(
            "failed before commit",
            errorEntry.Message,
            StringComparison.OrdinalIgnoreCase);
        AssertOnlyExpectedEntries(temporaryDirectory.Path, "document.json");
    }

    [Fact]
    public async Task FailedCommitPreservesOriginalBytesCleansTemporaryFileAndLogsError()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destinationPath = temporaryDirectory.GetPath("document.json");
        var originalBytes = "original-destination-bytes"u8.ToArray();
        await File.WriteAllBytesAsync(destinationPath, originalBytes);
        var logger = new RecordingLogger();
        var expectedException = new IOException("Deterministic commit failure.");

        var exception = await Assert.ThrowsAsync<IOException>(
            () => AtomicJsonFileWriter.WriteAsync(
                destinationPath,
                new TestDocument("replacement"),
                JsonOptions,
                overwrite: true,
                logger,
                new CommitFailureFileOperations(expectedException)));

        Assert.Same(expectedException, exception);
        Assert.Equal(
            originalBytes,
            await File.ReadAllBytesAsync(destinationPath));
        var errorEntry = Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Error);
        Assert.Same(expectedException, errorEntry.Exception);
        Assert.Contains(
            "commit failed",
            errorEntry.Message,
            StringComparison.OrdinalIgnoreCase);
        AssertOnlyExpectedEntries(temporaryDirectory.Path, "document.json");
    }

    [Fact]
    public async Task CleanupFailureIsLoggedSeparatelyAndDoesNotMaskCommitFailure()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destinationPath = temporaryDirectory.GetPath("document.json");
        var originalBytes = "original-destination-bytes"u8.ToArray();
        await File.WriteAllBytesAsync(destinationPath, originalBytes);
        var logger = new RecordingLogger();
        var commitException = new IOException("Deterministic commit failure.");
        var cleanupException = new IOException("Deterministic cleanup failure.");

        var exception = await Assert.ThrowsAsync<IOException>(
            () => AtomicJsonFileWriter.WriteAsync(
                destinationPath,
                new TestDocument("replacement"),
                JsonOptions,
                overwrite: true,
                logger,
                new CommitAndCleanupFailureFileOperations(
                    commitException,
                    cleanupException)));

        Assert.Same(commitException, exception);
        Assert.Equal(
            originalBytes,
            await File.ReadAllBytesAsync(destinationPath));
        var errorEntry = Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Error);
        Assert.Same(commitException, errorEntry.Exception);
        var warningEntry = Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning);
        Assert.Same(cleanupException, warningEntry.Exception);
        AssertOnlyExpectedEntries(temporaryDirectory.Path, "document.json");
    }

    [Fact]
    public async Task PreCommitCancellationPreservesOriginalCleansTemporaryFileAndDoesNotLogError()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destinationPath = temporaryDirectory.GetPath("document.json");
        var originalBytes = "original-destination-bytes"u8.ToArray();
        await File.WriteAllBytesAsync(destinationPath, originalBytes);
        using var cancellation = new CancellationTokenSource();
        var logger = new RecordingLogger();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => AtomicJsonFileWriter.WriteAsync(
                destinationPath,
                new TestDocument("replacement"),
                JsonOptions,
                overwrite: true,
                logger,
                new CancelAfterTemporaryWriteFileOperations(cancellation),
                cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(
            originalBytes,
            await File.ReadAllBytesAsync(destinationPath));
        Assert.DoesNotContain(
            logger.Entries,
            entry => entry.Level == LogLevel.Error);
        AssertOnlyExpectedEntries(temporaryDirectory.Path, "document.json");
    }

    [Fact]
    public async Task CancellationAfterCommitStartsDoesNotInterruptReplacement()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var destinationPath = temporaryDirectory.GetPath("document.json");
        await File.WriteAllBytesAsync(
            destinationPath,
            "original-destination-bytes"u8.ToArray());
        using var cancellation = new CancellationTokenSource();

        await AtomicJsonFileWriter.WriteAsync(
            destinationPath,
            new TestDocument("replacement"),
            JsonOptions,
            overwrite: true,
            NullLogger.Instance,
            new CancelDuringCommitFileOperations(cancellation),
            cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        AssertDocumentValue(destinationPath, "replacement");
        AssertOnlyExpectedEntries(temporaryDirectory.Path, "document.json");
    }

    private static void AssertDocumentValue(
        string destinationPath,
        string expectedValue)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(destinationPath));

        Assert.Equal(
            expectedValue,
            document.RootElement.GetProperty("Value").GetString());
    }

    private static void AssertOnlyExpectedEntries(
        string directoryPath,
        params string[] expectedFileNames)
    {
        var entries = Directory
            .EnumerateFileSystemEntries(directoryPath)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expected = expectedFileNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, entries);
        Assert.DoesNotContain(
            entries,
            name => name?.EndsWith(
                ".deadbelt.tmp",
                StringComparison.OrdinalIgnoreCase) == true);
    }

    private sealed record TestDocument(string Value);

    private sealed record UnsupportedDocument(
        string Value,
        Action Unsupported);

    private class RecordingAtomicFileOperations : IAtomicFileOperations
    {
        public int MoveNewCallCount { get; private set; }

        public int ReplaceCallCount { get; private set; }

        public virtual FileStream CreateTemporaryFile(string temporaryFilePath)
        {
            return new FileStream(
                temporaryFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public virtual void MoveNewFile(
            string sourcePath,
            string destinationPath)
        {
            MoveNewCallCount++;
            File.Move(
                sourcePath,
                destinationPath,
                overwrite: false);
        }

        public virtual void ReplaceFile(
            string sourcePath,
            string destinationPath)
        {
            ReplaceCallCount++;
            File.Replace(
                sourcePath,
                destinationPath,
                destinationBackupFileName: null);
        }

        public virtual void DeleteFile(string path)
        {
            File.Delete(path);
        }
    }

    private sealed class WriteFailureFileOperations
        : RecordingAtomicFileOperations
    {
        private readonly IOException _exception;

        public WriteFailureFileOperations(IOException exception)
        {
            _exception = exception;
        }

        public override FileStream CreateTemporaryFile(string temporaryFilePath)
        {
            return new FailingWriteFileStream(
                temporaryFilePath,
                _exception);
        }
    }

    private class CommitFailureFileOperations : RecordingAtomicFileOperations
    {
        private readonly IOException _exception;

        public CommitFailureFileOperations(IOException exception)
        {
            _exception = exception;
        }

        public override void ReplaceFile(
            string sourcePath,
            string destinationPath)
        {
            throw _exception;
        }
    }

    private sealed class CommitAndCleanupFailureFileOperations
        : CommitFailureFileOperations
    {
        private readonly IOException _cleanupException;

        public CommitAndCleanupFailureFileOperations(
            IOException commitException,
            IOException cleanupException)
            : base(commitException)
        {
            _cleanupException = cleanupException;
        }

        public override void DeleteFile(string path)
        {
            base.DeleteFile(path);
            throw _cleanupException;
        }
    }

    private sealed class CancelAfterTemporaryWriteFileOperations
        : RecordingAtomicFileOperations
    {
        private readonly CancellationTokenSource _cancellation;

        public CancelAfterTemporaryWriteFileOperations(
            CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public override FileStream CreateTemporaryFile(string temporaryFilePath)
        {
            return new CancelOnDisposeFileStream(
                temporaryFilePath,
                _cancellation);
        }
    }

    private sealed class CancelDuringCommitFileOperations
        : RecordingAtomicFileOperations
    {
        private readonly CancellationTokenSource _cancellation;

        public CancelDuringCommitFileOperations(
            CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public override void ReplaceFile(
            string sourcePath,
            string destinationPath)
        {
            _cancellation.Cancel();
            base.ReplaceFile(
                sourcePath,
                destinationPath);
        }
    }

    private sealed class CancelOnDisposeFileStream : FileStream
    {
        private readonly CancellationTokenSource _cancellation;

        public CancelOnDisposeFileStream(
            string path,
            CancellationTokenSource cancellation)
            : base(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough)
        {
            _cancellation = cancellation;
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            _cancellation.Cancel();
        }
    }

    private sealed class FailingWriteFileStream : FileStream
    {
        private readonly IOException _exception;

        public FailingWriteFileStream(
            string path,
            IOException exception)
            : base(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough)
        {
            _exception = exception;
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            throw _exception;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw _exception;
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return Task.FromException(_exception);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException(_exception);
        }
    }
}
