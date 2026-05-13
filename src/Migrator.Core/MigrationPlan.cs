using System.Text.Json;
using System.Text.Json.Serialization;

namespace Migrator.Core;

/// <summary>
/// Contrato de migración serializable (p. ej. JSON). El motor de ejecución puede vivir en consola o API;
/// en Blazor WebAssembly solo conviene manipular este modelo en el cliente.
/// </summary>
public sealed class MigrationPlan
{
    public SqlServerConnectionInfo Source { get; set; } = new();

    public SqlServerConnectionInfo Target { get; set; } = new();

    public List<TableMapping> Tables { get; set; } = new();
}

public sealed class TableMapping
{
    public string Source { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// En modo <c>upsert</c>, nombres de columna en el <strong>origen</strong> (se resuelven al destino con <see cref="Columns"/>)
    /// o, si no hay mapeo, se interpretan como nombres ya en el destino.
    /// </summary>
    public List<string> KeyColumns { get; set; } = new();

    public List<ColumnMapping> Columns { get; set; } = new();

    /// <summary>Valores típicos: insert, upsert, truncateReload.</summary>
    public string Mode { get; set; } = "insert";
}

public sealed class ColumnMapping
{
    public string Source { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;
}

public static class MigrationPlanJson
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions ReadOptions = new(WriteOptions)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(MigrationPlan plan) => JsonSerializer.Serialize(plan, WriteOptions);

    public static MigrationPlan Deserialize(string json) =>
        JsonSerializer.Deserialize<MigrationPlan>(json, ReadOptions)
        ?? new MigrationPlan();
}
