using Microsoft.EntityFrameworkCore;

namespace Migrator.Infrastructure.Data.Registry;

public sealed class MigrationRegistryDbContext : DbContext
{
    public MigrationRegistryDbContext(DbContextOptions<MigrationRegistryDbContext> options)
        : base(options)
    {
    }

    public DbSet<RegistryWorkPlanEntity> WorkPlans => Set<RegistryWorkPlanEntity>();

    public DbSet<RegistryTableProgressEntity> TableProgress => Set<RegistryTableProgressEntity>();

    public DbSet<RegistryTableExecutionHistoryEntity> TableExecutionHistory => Set<RegistryTableExecutionHistoryEntity>();

    public DbSet<RegistryAppStateEntity> AppState => Set<RegistryAppStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegistryWorkPlanEntity>(e =>
        {
            e.ToTable("RegistryWorkPlans");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.SourceKind).HasMaxLength(32);
            e.Property(x => x.SourceServer).HasMaxLength(256);
            e.Property(x => x.SourceDatabase).HasMaxLength(128);
            e.Property(x => x.TargetServer).HasMaxLength(256);
            e.Property(x => x.TargetDatabase).HasMaxLength(128);
            e.Property(x => x.SourceEndpointFingerprint).HasMaxLength(512);
            e.Property(x => x.TargetEndpointFingerprint).HasMaxLength(512);
            e.Property(x => x.SourceConnectionId).HasMaxLength(64);
            e.Property(x => x.TargetConnectionId).HasMaxLength(64);
            e.HasIndex(x => new
            {
                x.SourceKind,
                x.SourceEndpointFingerprint,
                x.TargetEndpointFingerprint,
            });
            e.HasMany(x => x.TableProgress)
                .WithOne(x => x.WorkPlan!)
                .HasForeignKey(x => x.WorkPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ExecutionHistory)
                .WithOne(x => x.WorkPlan!)
                .HasForeignKey(x => x.WorkPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RegistryTableProgressEntity>(e =>
        {
            e.ToTable("RegistryTableProgress");
            e.HasKey(x => x.Id);
            e.Property(x => x.SourceTable).HasMaxLength(512);
            e.Property(x => x.TargetTable).HasMaxLength(512);
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasIndex(x => new { x.WorkPlanId, x.SourceTable, x.TargetTable }).IsUnique();
        });

        modelBuilder.Entity<RegistryTableExecutionHistoryEntity>(e =>
        {
            e.ToTable("RegistryTableExecutionHistory");
            e.HasKey(x => x.Id);
            e.Property(x => x.SourceTable).HasMaxLength(512);
            e.Property(x => x.TargetTable).HasMaxLength(512);
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasIndex(x => new { x.WorkPlanId, x.ExecutedAt });
        });

        modelBuilder.Entity<RegistryAppStateEntity>(e =>
        {
            e.ToTable("RegistryAppState");
            e.HasKey(x => x.Id);
            // Fila singleton con Id=1 fijo; no debe ser IDENTITY en SQL Server.
            e.Property(x => x.Id).ValueGeneratedNever();
        });
    }
}
