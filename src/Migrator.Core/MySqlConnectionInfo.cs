namespace Migrator.Core;

/// <summary>Parámetros de conexión a MySQL (sin cadena única). La API construye la cadena internamente.</summary>
public sealed class MySqlConnectionInfo
{
    public string Server { get; set; } = string.Empty;

    /// <summary>Puerto TCP (por defecto 3306).</summary>
    public int Port { get; set; } = 3306;

    public string Database { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string? Password { get; set; }
}
