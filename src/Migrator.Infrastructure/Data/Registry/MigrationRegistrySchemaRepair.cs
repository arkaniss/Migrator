using Microsoft.EntityFrameworkCore;

namespace Migrator.Infrastructure.Data.Registry;

/// <summary>
/// Corrige esquemas legacy donde <c>RegistryAppState.Id</c> se creó como IDENTITY
/// (convención EF) pero la app inserta siempre <c>Id = 1</c>.
/// </summary>
public static class MigrationRegistrySchemaRepair
{
    public static async Task RepairAppStateIdentityColumnAsync(
        MigrationRegistryDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            IF OBJECT_ID(N'dbo.RegistryAppState', N'U') IS NOT NULL
               AND COLUMNPROPERTY(OBJECT_ID(N'dbo.RegistryAppState'), N'Id', 'IsIdentity') = 1
            BEGIN
                CREATE TABLE dbo.RegistryAppState_fix (
                    Id int NOT NULL,
                    LastExecutedWorkPlanId uniqueidentifier NULL,
                    LastExecutedAt datetimeoffset NULL,
                    CONSTRAINT PK_RegistryAppState_fix PRIMARY KEY (Id)
                );
                SET IDENTITY_INSERT dbo.RegistryAppState ON;
                INSERT INTO dbo.RegistryAppState_fix (Id, LastExecutedWorkPlanId, LastExecutedAt)
                SELECT Id, LastExecutedWorkPlanId, LastExecutedAt FROM dbo.RegistryAppState;
                SET IDENTITY_INSERT dbo.RegistryAppState OFF;
                DROP TABLE dbo.RegistryAppState;
                EXEC sp_rename N'dbo.RegistryAppState_fix', N'RegistryAppState';
            END
            """;

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
