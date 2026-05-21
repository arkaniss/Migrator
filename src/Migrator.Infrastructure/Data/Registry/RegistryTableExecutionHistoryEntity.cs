namespace Migrator.Infrastructure.Data.Registry;

public sealed class RegistryTableExecutionHistoryEntity
{
    public long Id { get; set; }

    public Guid WorkPlanId { get; set; }

    public RegistryWorkPlanEntity? WorkPlan { get; set; }

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
