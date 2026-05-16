namespace Migrator.Infrastructure.Data.Registry;

public sealed class RegistryTableProgressEntity
{
    public long Id { get; set; }

    public Guid WorkPlanId { get; set; }

    public RegistryWorkPlanEntity? WorkPlan { get; set; }

    public string SourceTable { get; set; } = string.Empty;

    public string TargetTable { get; set; } = string.Empty;

    /// <summary>completed, failed, dryRunCompleted, dryRunFailed</summary>
    public string Status { get; set; } = string.Empty;

    public long? RowsRead { get; set; }

    public long? RowsAffected { get; set; }

    public DateTimeOffset LastAttemptAt { get; set; }

    public DateTimeOffset? LastSuccessAt { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Fragmento JSON del <c>TableMapping</c> (opcional).</summary>
    public string? TableMappingJson { get; set; }
}
