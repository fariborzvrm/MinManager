using ObjectStorage.Domain.Interfaces;

namespace ObjectStorage.Domain.Entities;

public sealed class ServiceIdentity : IServiceIdentity
{
    public string Name { get; }
    public IReadOnlyList<string> AllowedPrefixes { get; }

    public ServiceIdentity(string name, IReadOnlyList<string> allowedPrefixes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Service name is required.", nameof(name));
        if (allowedPrefixes is null || allowedPrefixes.Count == 0)
            throw new ArgumentException("At least one allowed prefix is required.", nameof(allowedPrefixes));

        Name = name;
        AllowedPrefixes = allowedPrefixes;
    }

    public bool IsAuthorizedForObject(string objectKey)
    {
        return AllowedPrefixes.Any(prefix => objectKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
