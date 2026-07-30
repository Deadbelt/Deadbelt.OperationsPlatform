namespace Deadbelt.Domain.Doctor;

public sealed class DoctorModInventory
{
    public DoctorModInventory(
        string name,
        string path,
        bool isServerMod,
        bool directoryExists,
        string? publishedId,
        IEnumerable<string>? keyPaths = null,
        int declaredOrder = 0,
        bool addonsDirectoryExists = false,
        bool keysDirectoryExists = false,
        bool modMetadataExists = false,
        bool metaMetadataExists = false,
        int pboCount = 0,
        int bisignCount = 0,
        int bikeyCount = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Mod name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Mod path is required.", nameof(path));

        if (declaredOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(declaredOrder));

        if (pboCount < 0)
            throw new ArgumentOutOfRangeException(nameof(pboCount));

        if (bisignCount < 0)
            throw new ArgumentOutOfRangeException(nameof(bisignCount));

        if (bikeyCount < 0)
            throw new ArgumentOutOfRangeException(nameof(bikeyCount));

        Name = name.Trim();
        Path = path.Trim();
        IsServerMod = isServerMod;
        DirectoryExists = directoryExists;
        PublishedId = string.IsNullOrWhiteSpace(publishedId)
            ? null
            : publishedId.Trim();
        KeyPaths = Snapshot(keyPaths, nameof(keyPaths));
        DeclaredOrder = declaredOrder;
        AddonsDirectoryExists = addonsDirectoryExists;
        KeysDirectoryExists = keysDirectoryExists;
        ModMetadataExists = modMetadataExists;
        MetaMetadataExists = metaMetadataExists;
        PboCount = pboCount;
        BisignCount = bisignCount;
        BikeyCount = bikeyCount;
    }

    public string Name { get; }

    public string Path { get; }

    public bool IsServerMod { get; }

    public bool DirectoryExists { get; }

    public string? PublishedId { get; }

    public IReadOnlyList<string> KeyPaths { get; }

    public int DeclaredOrder { get; }

    public bool AddonsDirectoryExists { get; }

    public bool KeysDirectoryExists { get; }

    public bool ModMetadataExists { get; }

    public bool MetaMetadataExists { get; }

    public int PboCount { get; }

    public int BisignCount { get; }

    public int BikeyCount { get; }

    private static IReadOnlyList<string> Snapshot(
        IEnumerable<string>? values,
        string parameterName)
    {
        var snapshot = values?.ToArray() ?? [];

        if (snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Collection elements cannot be null or blank.",
                parameterName);
        }

        return Array.AsReadOnly(snapshot.Select(value => value.Trim()).ToArray());
    }
}
