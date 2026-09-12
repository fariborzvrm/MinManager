namespace ObjectStorage.Infrastructure.Entities;

public sealed class RegisteredService
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public string AllowedPrefixesRaw { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<string> GetAllowedPrefixes() =>
        AllowedPrefixesRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
