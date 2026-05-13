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

    public SqlServerConnectionInfo Connection { get; set; } = new();
}

public static class SavedConnectionFingerprint
{
    public static string Of(SqlServerConnectionInfo c)
    {
        var server = (c.Server ?? string.Empty).Trim().ToLowerInvariant();
        var db = (c.Database ?? string.Empty).Trim().ToLowerInvariant();
        var win = c.IntegratedSecurity;
        var user = win ? "" : (c.UserId ?? string.Empty).Trim().ToLowerInvariant();
        return $"{server}|{db}|{win}|{user}";
    }
}
