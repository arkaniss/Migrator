namespace Migrator.Infrastructure.Data.Registry;

/// <summary>Fila única (<c>Id=1</c>) con el último trabajo ejecutado registrado.</summary>
public sealed class RegistryAppStateEntity
{
    public int Id { get; set; }

    public Guid? LastExecutedWorkPlanId { get; set; }

    public DateTimeOffset? LastExecutedAt { get; set; }
}
