using Migrator.Core;

namespace Migrator.Infrastructure.Data.Registry;

public static class MigrationRegistryMapper
{
    public static (string SourceKind, string SrcServer, string SrcDb, string TgtServer, string TgtDb) SummarizeEndpoints(MigrationPlan plan)
    {
        var kind = (plan.SourceKind ?? "sqlServer").Trim();
        var (srcServer, srcDb) = PlanSourceEndpoint(plan);
        var tgtServer = plan.Target.Server?.Trim() ?? string.Empty;
        var tgtDb = plan.Target.Database?.Trim() ?? string.Empty;
        return (kind, srcServer, srcDb, tgtServer, tgtDb);
    }

    private static (string Server, string Database) PlanSourceEndpoint(MigrationPlan plan)
    {
        if (plan.UsesPostgresqlSource() && plan.PostgreSqlSource is not null)
        {
            return (plan.PostgreSqlSource.Server.Trim(), plan.PostgreSqlSource.Database.Trim());
        }

        if (plan.UsesMySqlSource() && plan.MySqlSource is not null)
        {
            return (plan.MySqlSource.Server.Trim(), plan.MySqlSource.Database.Trim());
        }

        if (plan.UsesOracleSource() && plan.OracleSource is not null)
        {
            return (plan.OracleSource.Server.Trim(), plan.OracleSource.ServiceName.Trim());
        }

        return (plan.Source.Server?.Trim() ?? string.Empty, plan.Source.Database?.Trim() ?? string.Empty);
    }

    public static RegistryWorkPlanSummaryDto ToSummaryDto(RegistryWorkPlanEntity e) =>
        new()
        {
            Id = e.Id,
            Name = e.Name,
            SourceKind = e.SourceKind,
            SourceServer = e.SourceServer,
            SourceDatabase = e.SourceDatabase,
            TargetServer = e.TargetServer,
            TargetDatabase = e.TargetDatabase,
            TableCount = e.TableCount,
            UpdatedAt = e.UpdatedAt,
        };

    public static RegistryTableProgressDto ToProgressDto(RegistryTableProgressEntity e) =>
        new()
        {
            SourceTable = e.SourceTable,
            TargetTable = e.TargetTable,
            Status = e.Status,
            RowsRead = e.RowsRead,
            RowsAffected = e.RowsAffected,
            LastAttemptAt = e.LastAttemptAt,
            LastSuccessAt = e.LastSuccessAt,
            ErrorMessage = e.ErrorMessage,
        };
}
