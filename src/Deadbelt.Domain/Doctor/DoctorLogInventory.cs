namespace Deadbelt.Domain.Doctor;

public sealed class DoctorLogInventory
{
    public DoctorLogInventory(
        string fullPath,
        string fileName,
        string logType,
        long fileSize,
        DateTime lastModifiedUtc,
        string sourceCategory)
    {
        FullPath = Required(fullPath, nameof(fullPath));
        FileName = Required(fileName, nameof(fileName));
        LogType = Required(logType, nameof(logType));
        SourceCategory = Required(sourceCategory, nameof(sourceCategory));

        if (fileSize < 0)
            throw new ArgumentOutOfRangeException(nameof(fileSize), fileSize, "File size cannot be negative.");

        if (lastModifiedUtc == default || lastModifiedUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Last-modified timestamp must be a non-default UTC value.",
                nameof(lastModifiedUtc));
        }

        FileSize = fileSize;
        LastModifiedUtc = lastModifiedUtc;
    }

    public string FullPath { get; }

    public string FileName { get; }

    public string LogType { get; }

    public long FileSize { get; }

    public DateTime LastModifiedUtc { get; }

    public string SourceCategory { get; }

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A non-blank value is required.", parameterName);

        return value.Trim();
    }
}
