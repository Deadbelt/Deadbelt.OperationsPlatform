using System.Security;

namespace Deadbelt.Infrastructure.Doctor;

internal sealed class OperatingSystemDoctorFileSystem : IDoctorFileSystem
{
    public DoctorPathInspection InspectFile(
        string path,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return new DoctorPathInspection(DoctorFileSystemStatus.Cancelled);

        try
        {
            var chainInspection = InspectPathChain(path, cancellationToken);

            if (chainInspection is not null)
                return chainInspection;

            var attributes = File.GetAttributes(path);

            if (attributes.HasFlag(FileAttributes.Directory))
                return new DoctorPathInspection(DoctorFileSystemStatus.Missing);

            var information = new FileInfo(path);

            return new DoctorPathInspection(
                DoctorFileSystemStatus.Available,
                attributes,
                information.Length,
                information.LastWriteTimeUtc);
        }
        catch (Exception exception)
        {
            return new DoctorPathInspection(Classify(exception, cancellationToken));
        }
    }

    public DoctorPathInspection InspectDirectory(
        string path,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return new DoctorPathInspection(DoctorFileSystemStatus.Cancelled);

        try
        {
            var chainInspection = InspectPathChain(path, cancellationToken);

            if (chainInspection is not null)
                return chainInspection;

            var attributes = File.GetAttributes(path);

            if (!attributes.HasFlag(FileAttributes.Directory))
                return new DoctorPathInspection(DoctorFileSystemStatus.Missing);

            return new DoctorPathInspection(
                DoctorFileSystemStatus.Available,
                attributes,
                LastModifiedUtc: new DirectoryInfo(path).LastWriteTimeUtc);
        }
        catch (Exception exception)
        {
            return new DoctorPathInspection(Classify(exception, cancellationToken));
        }
    }

    public DoctorTextReadResult ReadText(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (maximumBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        var inspection = InspectFile(path, cancellationToken);

        if (!inspection.IsAvailable)
        {
            return new DoctorTextReadResult(
                inspection.Status,
                DetectedSize: inspection.FileSize);
        }

        if (inspection.FileSize > maximumBytes)
        {
            return new DoctorTextReadResult(
                DoctorFileSystemStatus.TooLarge,
                DetectedSize: inspection.FileSize);
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            using var reader = new StreamReader(
                stream,
                detectEncodingFromByteOrderMarks: true);
            var content = new System.Text.StringBuilder(
                capacity: (int)Math.Min(maximumBytes, 4096));
            var buffer = new char[4096];

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new DoctorTextReadResult(
                        DoctorFileSystemStatus.Cancelled,
                        DetectedSize: stream.Length);
                }

                if (stream.Length > maximumBytes)
                {
                    return new DoctorTextReadResult(
                        DoctorFileSystemStatus.TooLarge,
                        DetectedSize: stream.Length);
                }

                var count = reader.Read(buffer, 0, buffer.Length);

                if (count == 0)
                    break;

                if ((long)content.Length + count > maximumBytes)
                {
                    return new DoctorTextReadResult(
                        DoctorFileSystemStatus.TooLarge,
                        DetectedSize: stream.Length);
                }

                content.Append(buffer, 0, count);
            }

            return new DoctorTextReadResult(
                DoctorFileSystemStatus.Available,
                content.ToString(),
                stream.Length);
        }
        catch (Exception exception)
        {
            return new DoctorTextReadResult(
                Classify(exception, cancellationToken),
                DetectedSize: inspection.FileSize);
        }
    }

    public DoctorDirectoryEnumerationResult EnumerateDirectory(
        string path,
        int maximumEntries,
        CancellationToken cancellationToken)
    {
        if (maximumEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));

        var entries = new List<DoctorFileSystemEntry>(
            Math.Min(maximumEntries, 256));

        try
        {
            using var enumerator = Directory
                .EnumerateFileSystemEntries(
                    path,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .GetEnumerator();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new DoctorDirectoryEnumerationResult(
                        DoctorFileSystemStatus.Cancelled,
                        entries);
                }

                if (entries.Count == maximumEntries)
                {
                    return new DoctorDirectoryEnumerationResult(
                        DoctorFileSystemStatus.Available,
                        entries,
                        LimitReached: true);
                }

                if (!enumerator.MoveNext())
                    break;

                entries.Add(InspectEntry(enumerator.Current, cancellationToken));
            }

            return new DoctorDirectoryEnumerationResult(
                DoctorFileSystemStatus.Available,
                entries);
        }
        catch (Exception exception)
        {
            return new DoctorDirectoryEnumerationResult(
                Classify(exception, cancellationToken),
                entries);
        }
    }

    private static DoctorFileSystemEntry InspectEntry(
        string path,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new DoctorFileSystemEntry(
                path,
                Path.GetFileName(path),
                IsDirectory: false,
                DoctorFileSystemStatus.Cancelled,
                0,
                null,
                null);
        }

        try
        {
            var attributes = File.GetAttributes(path);
            var isDirectory = attributes.HasFlag(FileAttributes.Directory);

            if (isDirectory)
            {
                return new DoctorFileSystemEntry(
                    path,
                    Path.GetFileName(path),
                    IsDirectory: true,
                    DoctorFileSystemStatus.Available,
                    attributes,
                    null,
                    new DirectoryInfo(path).LastWriteTimeUtc);
            }

            var information = new FileInfo(path);

            return new DoctorFileSystemEntry(
                path,
                Path.GetFileName(path),
                IsDirectory: false,
                DoctorFileSystemStatus.Available,
                attributes,
                information.Length,
                information.LastWriteTimeUtc);
        }
        catch (Exception exception)
        {
            return new DoctorFileSystemEntry(
                path,
                Path.GetFileName(path),
                IsDirectory: false,
                Classify(exception, cancellationToken),
                0,
                null,
                null);
        }
    }

    private static DoctorPathInspection? InspectPathChain(
        string path,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);

        if (string.IsNullOrEmpty(root))
            return new DoctorPathInspection(DoctorFileSystemStatus.InvalidPath);

        var relative = fullPath[root.Length..];
        var segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var current = root;

        foreach (var segment in segments)
        {
            if (cancellationToken.IsCancellationRequested)
                return new DoctorPathInspection(DoctorFileSystemStatus.Cancelled);

            current = Path.Combine(current, segment);

            try
            {
                var attributes = File.GetAttributes(current);

                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return new DoctorPathInspection(
                        DoctorFileSystemStatus.Available,
                        attributes);
                }
            }
            catch (Exception exception)
            {
                return new DoctorPathInspection(
                    Classify(exception, cancellationToken));
            }
        }

        return null;
    }

    private static DoctorFileSystemStatus Classify(
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException
            || cancellationToken.IsCancellationRequested)
        {
            return DoctorFileSystemStatus.Cancelled;
        }

        return exception switch
        {
            FileNotFoundException or DirectoryNotFoundException =>
                DoctorFileSystemStatus.Missing,
            ArgumentException or NotSupportedException or PathTooLongException =>
                DoctorFileSystemStatus.InvalidPath,
            UnauthorizedAccessException or SecurityException or IOException =>
                DoctorFileSystemStatus.Unreadable,
            _ => DoctorFileSystemStatus.Unreadable
        };
    }
}
