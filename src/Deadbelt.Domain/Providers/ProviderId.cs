namespace Deadbelt.Domain.Providers;

public readonly record struct ProviderId(Guid Value)
{
    public static ProviderId New()
    {
        return new ProviderId(Guid.NewGuid());
    }

    public static ProviderId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Provider ID cannot be empty.", nameof(value));

        return new ProviderId(value);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
