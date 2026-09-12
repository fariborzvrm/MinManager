namespace ObjectStorage.Application.Interfaces;

public interface IObjectKeyGenerator
{
    string Generate(string service, string category, string objectId, string fileName);
}
