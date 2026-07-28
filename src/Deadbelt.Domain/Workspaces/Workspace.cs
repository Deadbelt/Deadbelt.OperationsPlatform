namespace Deadbelt.Domain.Workspaces;

public sealed class Workspace
{
    public Workspace(
        string name,
        string path,
        string? description,
        DateTime createdUtc,
        string version)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workspace name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Workspace path is required.", nameof(path));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Workspace version is required.", nameof(version));

        Name = name.Trim();
        Path = path.Trim();
        Description = description?.Trim() ?? string.Empty;
        CreatedUtc = createdUtc;
        Version = version.Trim();
    }

    public string Name { get; }

    public string Path { get; }

    public string Description { get; }

    public DateTime CreatedUtc { get; }

    public string Version { get; }
}
