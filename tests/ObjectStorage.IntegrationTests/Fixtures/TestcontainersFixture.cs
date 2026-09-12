using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace ObjectStorage.IntegrationTests.Fixtures;

public sealed class TestcontainersFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private readonly IContainer _minio;

    public string PostgresConnectionString { get; private set; } = string.Empty;
    public string MinioEndpoint { get; private set; } = string.Empty;
    public string MinioAccessKey { get; private set; } = string.Empty;
    public string MinioSecretKey { get; private set; } = string.Empty;
    public string MinioBucket { get; } = "test-bucket";

    public const string TestApiKey = "test-api-key-integration";
    public const string TestServiceName = "test-service";
    public const string TestAllowedPrefix = "test-service/";

    public const string OtherServiceApiKey = "other-service-key-integration";
    public const string OtherServiceName = "other-service";
    public const string OtherServiceAllowedPrefix = "other-service/";

    public TestcontainersFixture()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("objectstorage_test")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();

        _minio = new ContainerBuilder()
            .WithImage("minio/minio:latest")
            .WithCommand("server", "/data", "--console-address", ":9001")
            .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
            .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin-dev-only")
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request =>
                    request.ForPath("/minio/health/live").ForPort(9000)))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _minio.StartAsync());

        PostgresConnectionString = _postgres.GetConnectionString();
        MinioEndpoint = $"{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}";
        MinioAccessKey = "minioadmin";
        MinioSecretKey = "minioadmin-dev-only";
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _minio.DisposeAsync().AsTask());
    }
}
