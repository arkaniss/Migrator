using System.Text.Json.Serialization;

namespace Migrator.Core;

public sealed class SaveRegistryWorkPlanRequest
{
    /// <summary>Si es null, se crea un nuevo plan.</summary>
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public MigrationPlan Plan { get; set; } = new();

    /// <summary>Id de preset de conexión origen (localStorage), sin credenciales.</summary>
    public string? SourceConnectionId { get; set; }

    /// <summary>Id de preset de conexión destino (localStorage).</summary>
    public string? TargetConnectionId { get; set; }

    /// <summary>Si true y no hay <see cref="Id"/>, busca un trabajo existente por endpoints y lo actualiza.</summary>
    public bool MatchByEndpoints { get; set; }
}

public sealed class RegistryWorkPlanMatchDto
{
    public bool Found { get; set; }

    public RegistryWorkPlanSummaryDto? Summary { get; set; }
}

public sealed class SaveRegistryWorkPlanResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RegistryWorkPlanSummaryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SourceKind { get; set; } = string.Empty;

    public string SourceServer { get; set; } = string.Empty;

    public string SourceDatabase { get; set; } = string.Empty;

    public string TargetServer { get; set; } = string.Empty;

    public string TargetDatabase { get; set; } = string.Empty;

    public string? SourceConnectionId { get; set; }

    public string? TargetConnectionId { get; set; }

    public int TableCount { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RegistryWorkPlanDetailDto
{
    public RegistryWorkPlanSummaryDto Summary { get; set; } = new();

    public MigrationPlan Plan { get; set; } = new();
}

public sealed class RegistryTableProgressDto
{
    public string SourceTable { get; set; } = string.Empty;

    public string TargetTable { get; set; } = string.Empty;

    /// <summary>completed, failed, dryRunCompleted, dryRunFailed</summary>
    public string Status { get; set; } = string.Empty;

    public long? RowsRead { get; set; }

    public long? RowsAffected { get; set; }

    public DateTimeOffset LastAttemptAt { get; set; }

    public DateTimeOffset? LastSuccessAt { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Mapeo guardado en la última ejecución registrada (si existe en BD).</summary>
    public TableMapping? Mapping { get; set; }
}

public sealed class RegistryTableExecutionHistoryDto
{
    public long Id { get; set; }

    public string SourceTable { get; set; } = string.Empty;

    public string TargetTable { get; set; } = string.Empty;

    public bool DryRun { get; set; }

    /// <summary>completed, failed, dryRunCompleted, dryRunFailed</summary>
    public string Status { get; set; } = string.Empty;

    public long? RowsRead { get; set; }

    public long? RowsAffected { get; set; }

    public DateTimeOffset ExecutedAt { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class RecordRegistryExecutionRequest
{
    public Guid WorkPlanId { get; set; }

    public bool DryRun { get; set; }

    public MigrationExecutionSummary Summary { get; set; } = new(false, Array.Empty<string>(), Array.Empty<TableMigrationResult>());

    /// <summary>Plan actual en cliente; se fusiona con el guardado (no reemplaza tablas previas).</summary>
    public MigrationPlan? UpdatePlanSnapshot { get; set; }

    public string? SourceConnectionId { get; set; }

    public string? TargetConnectionId { get; set; }
}

public sealed record RegistryLastExecutedDto(
    [property: JsonPropertyName("workPlanId")] Guid? WorkPlanId,
    [property: JsonPropertyName("executedAt")] DateTimeOffset? ExecutedAt);
