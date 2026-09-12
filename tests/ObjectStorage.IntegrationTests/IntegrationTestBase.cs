using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObjectStorage.Application.Configuration;
using ObjectStorage.Infrastructure.Configuration;
using ObjectStorage.Infrastructure.Entities;
using ObjectStorage.Infrastructure.Persistence;
using ObjectStorage.IntegrationTests.Fixtures;

namespace ObjectStorage.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<TestcontainersFixture>, IAsyncLifetime
{
    private readonly TestcontainersFixture _fixture;
    private WebApplicationFactory<Program>? _factory;

    protected HttpClient Client { get; private set; } = null!;
    protected WebApplicationFactory<Program> Factory => _factory!;

    protected IntegrationTestBase(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                builder.ConfigureServices(services =>
                {
                    // Remove existing DbContext registration
                    var descriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(DbContextOptions<ObjectStorageDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    // Override Postgres to use Testcontainers
                    services.AddDbContext<ObjectStorageDbContext>(options =>
                        options.UseNpgsql(_fixture.PostgresConnectionString));

                    // Override MinIO options
                    services.Configure<MinioOptions>(opt =>
                    {
                        opt.Endpoint = _fixture.MinioEndpoint;
                        opt.AccessKey = _fixture.MinioAccessKey;
                        opt.SecretKey = _fixture.MinioSecretKey;
                        opt.Bucket = _fixture.MinioBucket;
                        opt.UseSSL = false;
                    });

                    // Override Storage options
                    services.Configure<StorageOptions>(opt =>
                    {
                        opt.DefaultService = "test-service";
                        opt.AllowedCategories = ["documents", "images", "invoices"];
                        opt.MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB for tests
                        opt.AllowedContentTypes =
                            ["application/pdf", "image/png", "image/jpeg", "text/plain", "application/octet-stream"];
                        opt.MaxPresignedUrlExpirationSeconds = 3600;
                    });
                });
            });

        // Force host to start (applies migrations via Program.cs)
        Client = _factory.CreateClient();
        Client.DefaultRequestHeaders.Add("X-Api-Key", TestcontainersFixture.TestApiKey);

        // Seed test service after migrations have run
        await SeedTestServiceAsync();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async Task SeedTestServiceAsync()
    {
        using var scope = _factory!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ObjectStorageDbContext>();

        if (!await context.RegisteredServices.AnyAsync())
        {
            // Primary test service
            context.RegisteredServices.Add(new RegisteredService
            {
                Id = Guid.NewGuid(),
                Name = TestcontainersFixture.TestServiceName,
                ApiKeyHash = ComputeHash(TestcontainersFixture.TestApiKey),
                AllowedPrefixesRaw = TestcontainersFixture.TestAllowedPrefix,
                Active = true,
                CreatedAt = DateTimeOffset.UtcNow
            });

            // Secondary service for cross-service authorization tests
            context.RegisteredServices.Add(new RegisteredService
            {
                Id = Guid.NewGuid(),
                Name = TestcontainersFixture.OtherServiceName,
                ApiKeyHash = ComputeHash(TestcontainersFixture.OtherServiceApiKey),
                AllowedPrefixesRaw = TestcontainersFixture.OtherServiceAllowedPrefix,
                Active = true,
                CreatedAt = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();
        }
    }

    private static string ComputeHash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
