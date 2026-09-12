using ObjectStorage.Application.Interfaces;

namespace ObjectStorage.Infrastructure.Services;

public sealed class UlidObjectIdGenerator : IObjectIdGenerator
{
    public string Generate() => Ulid.NewUlid().ToString();
}
