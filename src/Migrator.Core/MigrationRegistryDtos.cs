using System.Text.Json.Serialization;

namespace Migrator.Core;

public sealed class SaveRegistryWorkPlanRequest
{
    /// <summary>Si es null, se crea un nuevo plan.</summary>
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public MigrationPlan Plan { get; set; } = new();
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
}

public sealed class RecordRegistryExecutionRequest
{
    public Guid WorkPlanId { get; set; }

    public bool DryRun { get; set; }

    public MigrationExecutionSummary Summary { get; set; } = new(false, Array.Empty<string>(), Array.Empty<TableMigrationResult>());

    /// <summary>Plan completo actual en cliente; actualiza <c>PlanJson</c> del trabajo.</summary>
    public MigrationPlan? UpdatePlanSnapshot { get; set; }
}

public sealed record RegistryLastExecutedDto(
    [property: JsonPropertyName("workPlanId")] Guid? WorkPlanId,
    [property: JsonPropertyName("executedAt")] DateTimeOffset? ExecutedAt);
