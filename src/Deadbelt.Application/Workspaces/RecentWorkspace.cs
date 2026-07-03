namespace Deadbelt.Application.Workspaces;

public sealed class RecentWorkspace
{
    public RecentWorkspace(
        string name,
        string path,
        DateTime lastOpenedUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Recent workspace name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Recent workspace path is required.", nameof(path));

        Name = name.Trim();
        Path = path.Trim();
        LastOpenedUtc = lastOpenedUtc;
    }

    public string Name { get; }

    public string Path { get; }

    public DateTime LastOpenedUtc { get; }

    public string LastOpenedDisplay => LastOpenedUtc.ToString("u");
}