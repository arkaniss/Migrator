namespace Migrator.Core;
/// <summary>
/// Parámetros de conexión a SQL Server (sin cadena única). El motor y la API construyen la cadena internamente.
/// </summary>
public sealed class SqlServerConnectionInfo
{
    public string Server { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    /// <summary>Windows / integrada cuando es true; SQL Server cuando es false.</summary>
    public bool IntegratedSecurity { get; set; } = true;

    public string? UserId { get; set; }

    public string? Password { get; set; }

    public bool TrustServerCertificate { get; set; } = true;

    public bool Encrypt { get; set; } = true;
}
