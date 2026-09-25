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

    public static async Task RepairWorkPlanAndHistorySchemaAsync(
        MigrationRegistryDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            IF OBJECT_ID(N'dbo.RegistryWorkPlans', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.RegistryWorkPlans', N'SourceEndpointFingerprint') IS NULL
                    ALTER TABLE dbo.RegistryWorkPlans ADD SourceEndpointFingerprint nvarchar(512) NOT NULL
                        CONSTRAINT DF_RegistryWorkPlans_SourceFp DEFAULT N'';
                IF COL_LENGTH(N'dbo.RegistryWorkPlans', N'TargetEndpointFingerprint') IS NULL
                    ALTER TABLE dbo.RegistryWorkPlans ADD TargetEndpointFingerprint nvarchar(512) NOT NULL
                        CONSTRAINT DF_RegistryWorkPlans_TargetFp DEFAULT N'';
                IF COL_LENGTH(N'dbo.RegistryWorkPlans', N'SourceConnectionId') IS NULL
                    ALTER TABLE dbo.RegistryWorkPlans ADD SourceConnectionId nvarchar(64) NULL;
                IF COL_LENGTH(N'dbo.RegistryWorkPlans', N'TargetConnectionId') IS NULL
                    ALTER TABLE dbo.RegistryWorkPlans ADD TargetConnectionId nvarchar(64) NULL;
            END

            IF OBJECT_ID(N'dbo.RegistryTableExecutionHistory', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.RegistryTableExecutionHistory (
                    Id bigint IDENTITY(1,1) NOT NULL,
                    WorkPlanId uniqueidentifier NOT NULL,
                    SourceTable nvarchar(512) NOT NULL,
                    TargetTable nvarchar(512) NOT NULL,
                    DryRun bit NOT NULL,
                    Status nvarchar(32) NOT NULL,
                    RowsRead bigint NULL,
                    RowsAffected bigint NULL,
                    ExecutedAt datetimeoffset NOT NULL,
                    ErrorMessage nvarchar(max) NULL,
                    CONSTRAINT PK_RegistryTableExecutionHistory PRIMARY KEY (Id),
                    CONSTRAINT FK_RegistryTableExecutionHistory_WorkPlan
                        FOREIGN KEY (WorkPlanId) REFERENCES dbo.RegistryWorkPlans(Id) ON DELETE CASCADE
                );
                CREATE INDEX IX_RegistryTableExecutionHistory_WorkPlanExecuted
                    ON dbo.RegistryTableExecutionHistory (WorkPlanId, ExecutedAt DESC);
            END

            IF OBJECT_ID(N'dbo.RegistryWorkPlans', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_RegistryWorkPlans_Endpoints'
                      AND object_id = OBJECT_ID(N'dbo.RegistryWorkPlans'))
            BEGIN
                CREATE INDEX IX_RegistryWorkPlans_Endpoints ON dbo.RegistryWorkPlans
                    (SourceKind, SourceEndpointFingerprint, TargetEndpointFingerprint);
            END
            """;

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
