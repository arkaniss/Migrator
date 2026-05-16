namespace Migrator.Infrastructure.Data.Registry;

public sealed class RegistryWorkPlanEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Tipo de motor de origen (alineado con <c>Migrator.Core.MigrationPlan.SourceKind</c>): sqlServer, mySql, oracle, postgresql.</summary>
    public string SourceKind { get; set; } = "sqlServer";

    public string SourceServer { get; set; } = string.Empty;

    public string SourceDatabase { get; set; } = string.Empty;

    public string TargetServer { get; set; } = string.Empty;

    public string TargetDatabase { get; set; } = string.Empty;

    public int TableCount { get; set; }

    public string PlanJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<RegistryTableProgressEntity> TableProgress { get; set; } = new List<RegistryTableProgressEntity>();
}
