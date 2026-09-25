namespace Migrator.Core;

/// <summary>Parámetros de conexión a PostgreSQL (sin cadena única). La API construye la cadena internamente.</summary>
public sealed class PostgreSqlConnectionInfo
{
    public string Server { get; set; } = string.Empty;

    /// <summary>Puerto TCP (por defecto 5432).</summary>
    public int Port { get; set; } = 5432;

    public string Database { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string? Password { get; set; }
}
