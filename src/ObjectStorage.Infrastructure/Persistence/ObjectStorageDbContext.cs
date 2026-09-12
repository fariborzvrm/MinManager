using Microsoft.EntityFrameworkCore;
using ObjectStorage.Application.Entities;
using ObjectStorage.Domain.Entities;
using ObjectStorage.Infrastructure.Entities;

namespace ObjectStorage.Infrastructure.Persistence;

public sealed class ObjectStorageDbContext : DbContext
{
    public ObjectStorageDbContext(DbContextOptions<ObjectStorageDbContext> options)
        : base(options)
    {
    }

    public DbSet<StoredObject> StoredObjects => Set<StoredObject>();
    public DbSet<RegisteredService> RegisteredServices => Set<RegisteredService>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredObject>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.ObjectKey).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Size);
            entity.Property(e => e.CreatedAt);
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasIndex(e => e.ObjectKey).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<RegisteredService>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ApiKeyHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.AllowedPrefixesRaw).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.Active).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.ApiKeyHash).IsUnique();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ObjectId).HasMaxLength(64);
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ObjectId).HasMaxLength(64);
            entity.Property(e => e.Result).IsRequired().HasMaxLength(32);
            entity.Property(e => e.CorrelationId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.Metadata).HasMaxLength(2048);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.ServiceName);
            entity.HasIndex(e => e.CorrelationId);
        });
    }
}
