using System.Text.Json;
using System.Text.Json.Serialization;

namespace Migrator.Core;

/// <summary>
/// Contrato de migración serializable (p. ej. JSON). El motor de ejecución puede vivir en consola o API;
/// en Blazor WebAssembly solo conviene manipular este modelo en el cliente.
/// </summary>
public sealed class MigrationPlan
{
    /// <summary>Origen: <c>sqlServer</c> (por defecto), <c>mySql</c> u <c>oracle</c>.</summary>
    public string SourceKind { get; set; } = "sqlServer";

    public SqlServerConnectionInfo Source { get; set; } = new();

    /// <summary>Origen MySQL cuando <see cref="SourceKind"/> es <c>mySql</c>.</summary>
    public MySqlConnectionInfo? MySqlSource { get; set; }

    /// <summary>Origen Oracle cuando <see cref="SourceKind"/> es <c>oracle</c>.</summary>
    public OracleConnectionInfo? OracleSource { get; set; }

    public SqlServerConnectionInfo Target { get; set; } = new();

    public List<TableMapping> Tables { get; set; } = new();

    public bool UsesMySqlSource() =>
        string.Equals(SourceKind?.Trim(), "mySql", StringComparison.OrdinalIgnoreCase);

    public bool UsesOracleSource() =>
        string.Equals(SourceKind?.Trim(), "oracle", StringComparison.OrdinalIgnoreCase);
}

public sealed class TableMapping
{
    public string Source { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Si es true, intenta ejecutar <c>DBCC CHECKIDENT(..., RESEED, 1)</c> en la tabla destino (solo si tiene IDENTITY).
    /// Útil cuando haces recargas y quieres reiniciar el contador.
    /// </summary>
    public bool ReseedToZero { get; set; }

    /// <summary>
    /// Si es true, en <c>insert</c>, <c>truncateReload</c> y <c>upsert</c> intenta conservar en el destino los valores
    /// de la columna IDENTITY que vengan del origen (mapeada en <see cref="Columns"/>): <c>SqlBulkCopy</c> con
    /// <c>KeepIdentity</c> y, en upsert, <c>SET IDENTITY_INSERT</c> alrededor del <c>MERGE</c> cuando aplica.
    /// </summary>
    public bool PreserveIdentityValues { get; set; }

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
    /// <summary>
    /// Nombre de la columna en el origen. Puede dejarse vacío si <see cref="SourceExpression"/> define
    /// todo el fragmento del SELECT (p. ej. literal <c>NULL</c> o <c>'texto'</c> sin usar <c>s.</c>).
    /// </summary>
    public string Source { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Fragmento SQL opcional usado en el SELECT del origen en lugar del nombre entre corchetes o backticks.
    /// Si <see cref="Source"/> está vacío, aquí va el fragmento completo (p. ej. literal <c>0</c> o <c>N'x'</c>).
    /// Si hay <see cref="Source"/>, suele referenciar la tabla con el alias <c>s</c> (p. ej. SQL Server: <c>ISNULL(s.[col], 0)</c>; MySQL: <c>IFNULL(s.`col`,0)</c>; Oracle: identificador entre comillas dobles con prefijo <c>s.</c>).
    /// </summary>
    public string? SourceExpression { get; set; }
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
