namespace Migrator.Core;

/// <summary>Parámetros de conexión a Oracle (Easy Connect: host:puerto/servicio). La API construye la cadena internamente.</summary>
public sealed class OracleConnectionInfo
{
    public string Server { get; set; } = string.Empty;

    /// <summary>Puerto TCP (por defecto 1521).</summary>
    public int Port { get; set; } = 1521;

    /// <summary>Nombre de servicio o PDB (p. ej. <c>ORCL</c>, <c>XEPDB1</c>).</summary>
    public string ServiceName { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string? Password { get; set; }
}
