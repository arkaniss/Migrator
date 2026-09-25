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

    /// <summary>Huella origen sin credenciales (ver <see cref="Migrator.Core.RegistryEndpointFingerprint"/>).</summary>
    public string SourceEndpointFingerprint { get; set; } = string.Empty;

    /// <summary>Huella destino sin credenciales.</summary>
    public string TargetEndpointFingerprint { get; set; } = string.Empty;

    /// <summary>Id de preset de conexión origen en el cliente (localStorage).</summary>
    public string? SourceConnectionId { get; set; }

    /// <summary>Id de preset de conexión destino en el cliente.</summary>
    public string? TargetConnectionId { get; set; }

    public int TableCount { get; set; }

    public string PlanJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<RegistryTableProgressEntity> TableProgress { get; set; } = new List<RegistryTableProgressEntity>();

    public ICollection<RegistryTableExecutionHistoryEntity> ExecutionHistory { get; set; } =
        new List<RegistryTableExecutionHistoryEntity>();
}
