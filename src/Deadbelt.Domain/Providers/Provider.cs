namespace Deadbelt.Domain.Providers;

public sealed class Provider
{
    public const string CurrentVersion = "0.1";

    public Provider(
        ProviderId id,
        string workspacePath,
        string name,
        ProviderType providerType,
        ProviderStatus status,
        DateTime createdUtc,
        string version)
    {
        if (id.Value == Guid.Empty)
            throw new ArgumentException("Provider ID cannot be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(workspacePath))
            throw new ArgumentException("Workspace path is required.", nameof(workspacePath));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Provider name is required.", nameof(name));

        if (providerType == ProviderType.Unknown)
            throw new ArgumentException("Provider type is required.", nameof(providerType));

        if (status == ProviderStatus.Unknown)
            throw new ArgumentException("Provider status is required.", nameof(status));

        Id = id;
        WorkspacePath = workspacePath.Trim();
        Name = name.Trim();
        ProviderType = providerType;
        Status = status;
        CreatedUtc = createdUtc.Kind == DateTimeKind.Utc
            ? createdUtc
            : createdUtc.ToUniversalTime();
        Version = string.IsNullOrWhiteSpace(version)
            ? CurrentVersion
            : version.Trim();
    }

    public ProviderId Id { get; }

    public string WorkspacePath { get; }

    public string Name { get; }

    public ProviderType ProviderType { get; }

    public ProviderStatus Status { get; }

    public DateTime CreatedUtc { get; }

    public string Version { get; }

    public static Provider Create(
        string workspacePath,
        string name,
        ProviderType providerType)
    {
        return new Provider(
            ProviderId.New(),
            workspacePath,
            name,
            providerType,
            ProviderStatus.Draft,
            DateTime.UtcNow,
            CurrentVersion);
    }
}
