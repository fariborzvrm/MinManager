namespace ObjectStorage.Domain.Interfaces;

public interface IServiceIdentity
{
    string Name { get; }
    IReadOnlyList<string> AllowedPrefixes { get; }
    bool IsAuthorizedForObject(string objectKey);
}
