using Migrator.Core;

namespace Migrator.Web.Models;

/// <summary>Instantánea del plan guardada en el navegador (localStorage).</summary>
public sealed class PlanSnapshotEnvelope
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;

    public MigrationPlan Plan { get; set; } = new();

    public List<TableRunLogEntry> RunLog { get; set; } = new();
}

public sealed class TableRunLogEntry
{
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;

    public bool DryRun { get; set; }

    public string Source { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? Details { get; set; }
}

public sealed class TableRunState
{
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;

    public bool Success { get; set; }

    public bool DryRun { get; set; }

    public string? Message { get; set; }
}
