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

    public static void ApplyEndpointFields(RegistryWorkPlanEntity entity, MigrationPlan plan)
    {
        var (kind, ss, sd, ts, td) = SummarizeEndpoints(plan);
        entity.SourceKind = kind;
        entity.SourceServer = ss;
        entity.SourceDatabase = sd;
        entity.TargetServer = ts;
        entity.TargetDatabase = td;
        entity.SourceEndpointFingerprint = RegistryEndpointFingerprint.OfSource(plan);
        entity.TargetEndpointFingerprint = RegistryEndpointFingerprint.OfTarget(plan);
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
            SourceConnectionId = e.SourceConnectionId,
            TargetConnectionId = e.TargetConnectionId,
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
            Mapping = DeserializeTableMapping(e.TableMappingJson),
        };

    private static TableMapping? DeserializeTableMapping(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<TableMapping>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    public static RegistryTableExecutionHistoryDto ToHistoryDto(RegistryTableExecutionHistoryEntity e) =>
        new()
        {
            Id = e.Id,
            SourceTable = e.SourceTable,
            TargetTable = e.TargetTable,
            DryRun = e.DryRun,
            Status = e.Status,
            RowsRead = e.RowsRead,
            RowsAffected = e.RowsAffected,
            ExecutedAt = e.ExecutedAt,
            ErrorMessage = e.ErrorMessage,
        };
}
