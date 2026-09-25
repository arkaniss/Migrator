using System.Globalization;
using System.Text.Json.Serialization;
using Migrator.Core;

namespace Migrator.Web.Models;

/// <summary>Raíz JSON para importar/exportar conexiones guardadas (localStorage o archivo).</summary>
public sealed class SavedConnectionsFile
{
    public int Version { get; set; } = 1;

    public List<SavedConnectionEntry> Connections { get; set; } = new();
}

public sealed class SavedConnectionEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Nombre visible en listas.</summary>
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary><c>sqlServer</c>, <c>mySql</c>, <c>oracle</c> o <c>postgresql</c>.</summary>
    public string Kind { get; set; } = "sqlServer";

    /// <summary>Conexión SQL Server cuando <see cref="Kind"/> es <c>sqlServer</c> (nombre JSON histórico: connection).</summary>
    [JsonPropertyName("connection")]
    public SqlServerConnectionInfo? Connection { get; set; }

    /// <summary>Conexión MySQL cuando <see cref="Kind"/> es <c>mySql</c>.</summary>
    public MySqlConnectionInfo? MySql { get; set; }

    /// <summary>Conexión Oracle cuando <see cref="Kind"/> es <c>oracle</c>.</summary>
    public OracleConnectionInfo? Oracle { get; set; }

    /// <summary>Conexión PostgreSQL cuando <see cref="Kind"/> es <c>postgresql</c>.</summary>
    public PostgreSqlConnectionInfo? PostgreSql { get; set; }
}

public static class SavedConnectionFingerprint
{
    public static string Of(SqlServerConnectionInfo c)
    {
        var server = (c.Server ?? string.Empty).Trim().ToLowerInvariant();
        var db = (c.Database ?? string.Empty).Trim().ToLowerInvariant();
        var win = c.IntegratedSecurity;
        var user = win ? "" : (c.UserId ?? string.Empty).Trim().ToLowerInvariant();
        return $"sql|{server}|{db}|{win}|{user}";
    }

    public static string Of(MySqlConnectionInfo c)
    {
        var server = (c.Server ?? string.Empty).Trim().ToLowerInvariant();
        var port = c.Port.ToString(CultureInfo.InvariantCulture);
        var db = (c.Database ?? string.Empty).Trim().ToLowerInvariant();
        var user = (c.UserId ?? string.Empty).Trim().ToLowerInvariant();
        return $"mysql|{server}|{port}|{db}|{user}";
    }

    public static string Of(OracleConnectionInfo c)
    {
        var server = (c.Server ?? string.Empty).Trim().ToLowerInvariant();
        var port = c.Port.ToString(CultureInfo.InvariantCulture);
        var svc = (c.ServiceName ?? string.Empty).Trim().ToLowerInvariant();
        var user = (c.UserId ?? string.Empty).Trim().ToLowerInvariant();
        return $"oracle|{server}|{port}|{svc}|{user}";
    }

    public static string Of(PostgreSqlConnectionInfo c)
    {
        var server = (c.Server ?? string.Empty).Trim().ToLowerInvariant();
        var port = c.Port.ToString(CultureInfo.InvariantCulture);
        var db = (c.Database ?? string.Empty).Trim().ToLowerInvariant();
        var user = (c.UserId ?? string.Empty).Trim().ToLowerInvariant();
        return $"postgresql|{server}|{port}|{db}|{user}";
    }
}
