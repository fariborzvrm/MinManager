using System.Security.Cryptography;
using System.Text;
using ObjectStorage.Domain.Entities;
using ObjectStorage.Domain.Interfaces;
using ObjectStorage.Infrastructure.Persistence;

namespace ObjectStorage.Infrastructure.Services;

public sealed class ServiceResolver : IServiceResolver
{
    private readonly IRegisteredServiceRepository _repository;

    public ServiceResolver(IRegisteredServiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<IServiceIdentity?> ResolveAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var hash = ComputeHash(apiKey);
        var registered = await _repository.GetByApiKeyHashAsync(hash, cancellationToken);

        if (registered is null)
            return null;

        var prefixes = registered.GetAllowedPrefixes();
        if (prefixes.Count == 0)
            return null;

        return new ServiceIdentity(registered.Name, prefixes);
    }

    public static string ComputeHash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
