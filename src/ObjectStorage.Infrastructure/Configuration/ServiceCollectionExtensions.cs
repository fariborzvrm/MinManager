using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObjectStorage.Application.Configuration;
using ObjectStorage.Application.Interfaces;
using ObjectStorage.Application.Services;
using ObjectStorage.Domain.Interfaces;
using ObjectStorage.Infrastructure.Configuration;
using ObjectStorage.Infrastructure.MinIO;
using ObjectStorage.Infrastructure.Persistence;
using ObjectStorage.Infrastructure.Services;

namespace ObjectStorage.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddObjectStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MinioOptions>()
            .Bind(configuration.GetSection(MinioOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<MinioObjectStorage>();
        services.AddSingleton<IObjectStorage>(sp => sp.GetRequiredService<MinioObjectStorage>());
        services.AddSingleton<MinioHealthCheck>();

        services.AddDbContext<ObjectStorageDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<IObjectMetadataRepository, EfObjectMetadataRepository>();
        services.AddScoped<IIdempotencyRepository, EfIdempotencyRepository>();
        services.AddScoped<IRegisteredServiceRepository, EfRegisteredServiceRepository>();
        services.AddScoped<IAuditLogger, EfAuditLogRepository>();
        services.AddSingleton<IObjectIdGenerator, UlidObjectIdGenerator>();
        services.AddSingleton<IObjectKeyGenerator, HierarchicalObjectKeyGenerator>();
        services.AddScoped<IServiceResolver, ServiceResolver>();
        services.AddScoped<ObjectService>();
        services.AddScoped<PostgresHealthCheck>();

        return services;
    }
}