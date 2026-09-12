namespace ObjectStorage.Domain.Interfaces;

public interface IServiceResolver
{
    Task<IServiceIdentity?> ResolveAsync(string apiKey, CancellationToken cancellationToken = default);
}
