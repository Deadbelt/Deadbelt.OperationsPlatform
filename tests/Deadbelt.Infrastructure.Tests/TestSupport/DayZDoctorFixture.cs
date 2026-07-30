using System.Collections.ObjectModel;

namespace Deadbelt.Infrastructure.Tests.TestSupport;

internal sealed class DayZDoctorFixture : IDisposable
{
    private readonly TemporaryDirectory _temporaryDirectory = new();

    public string RootPath => _temporaryDirectory.Path;

    public string GetPath(params string[] segments)
    {
        return _temporaryDirectory.GetPath(segments);
    }

    public string AddExecutable()
    {
        return AddFile(
            "DayZServer_x64.exe",
            "synthetic executable");
    }

    public string AddStartup(
        string name,
        string content)
    {
        return AddFile(name, content);
    }

    public string AddConfiguration(
        string name,
        string content)
    {
        return AddFile(name, content);
    }

    public string AddMission(
        string template,
        string typesXml = "<types />",
        string settingsJson = "{}")
    {
        AddFile(
            Path.Combine("mpmissions", template, "init.c"),
            "void main() {}");
        AddFile(
            Path.Combine("mpmissions", template, "db", "types.xml"),
            typesXml);
        AddFile(
            Path.Combine("mpmissions", template, "description.ext"),
            "respawn = 3;");
        AddFile(
            Path.Combine("mpmissions", template, "cfgeconomycore.xml"),
            "<economycore />");
        AddFile(
            Path.Combine("mpmissions", template, "cfggameplay.json"),
            "{}");

        foreach (var fileName in new[]
                 {
                     "events.xml",
                     "globals.xml",
                     "economy.xml",
                     "messages.xml"
                 })
        {
            AddFile(
                Path.Combine("mpmissions", template, "db", fileName),
                "<root />");
        }

        AddFile(
            Path.Combine("mpmissions", template, "settings.json"),
            settingsJson);

        return GetPath("mpmissions", template);
    }

    public string AddMod(
        string directoryName,
        string displayName,
        string publishedId,
        string? keyName = null)
    {
        AddFile(
            Path.Combine(directoryName, "meta.cpp"),
            $"publishedid = \"{publishedId}\";");
        AddFile(
            Path.Combine(directoryName, "mod.cpp"),
            $"name = \"{displayName}\";");
        AddFile(
            Path.Combine(directoryName, "addons", "content.pbo"),
            "synthetic pbo");
        AddFile(
            Path.Combine(directoryName, "addons", "content.pbo.synthetic.bisign"),
            "synthetic signature");

        if (keyName is not null)
        {
            AddFile(
                Path.Combine(directoryName, "keys", keyName),
                "synthetic public key");
        }

        return GetPath(directoryName);
    }

    public string AddGlobalKey(string keyName)
    {
        return AddFile(
            Path.Combine("keys", keyName),
            "synthetic public key");
    }

    public string AddDirectory(params string[] segments)
    {
        var path = GetPath(segments);
        Directory.CreateDirectory(path);
        return path;
    }

    public string AddFile(
        string relativePath,
        string content)
    {
        var path = GetPath(relativePath);
        var parent = Path.GetDirectoryName(path);

        if (parent is not null)
            Directory.CreateDirectory(parent);

        File.WriteAllText(path, content);
        return path;
    }

    public DayZFixtureSnapshot CaptureSnapshot()
    {
        var directories = Directory
            .EnumerateDirectories(
                RootPath,
                "*",
                SearchOption.AllDirectories)
            .Prepend(RootPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToDictionary(
                path => string.Equals(path, RootPath, StringComparison.Ordinal)
                    ? "."
                    : Relative(path),
                path => new DayZFixtureDirectorySnapshot(
                    File.GetAttributes(path),
                    Directory.GetLastWriteTimeUtc(path)),
                StringComparer.Ordinal);
        var files = Directory
            .EnumerateFiles(
                RootPath,
                "*",
                SearchOption.AllDirectories)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToDictionary(
                Relative,
                path => new DayZFixtureFileSnapshot(
                    File.ReadAllBytes(path),
                    File.GetAttributes(path),
                    File.GetLastWriteTimeUtc(path)),
                StringComparer.Ordinal);

        return new DayZFixtureSnapshot(
            new ReadOnlyDictionary<string, DayZFixtureDirectorySnapshot>(directories),
            new ReadOnlyDictionary<string, DayZFixtureFileSnapshot>(files));
    }

    public void Dispose()
    {
        _temporaryDirectory.Dispose();
    }

    private string Relative(string path)
    {
        return Path.GetRelativePath(
            RootPath,
            path);
    }
}

internal sealed record DayZFixtureSnapshot(
    IReadOnlyDictionary<string, DayZFixtureDirectorySnapshot> Directories,
    IReadOnlyDictionary<string, DayZFixtureFileSnapshot> Files);

internal sealed record DayZFixtureDirectorySnapshot(
    FileAttributes Attributes,
    DateTime LastWriteUtc);

internal sealed record DayZFixtureFileSnapshot(
    byte[] Content,
    FileAttributes Attributes,
    DateTime LastWriteUtc);
