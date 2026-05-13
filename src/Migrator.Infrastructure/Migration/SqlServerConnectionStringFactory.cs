using Microsoft.Data.SqlClient;
using Migrator.Core;

namespace Migrator.Infrastructure.Migration;

public static class SqlServerConnectionStringFactory
{
    public static string Build(SqlServerConnectionInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (string.IsNullOrWhiteSpace(info.Server) || string.IsNullOrWhiteSpace(info.Database))
        {
            throw new InvalidOperationException("Server y Database son obligatorios para construir la cadena de conexión.");
        }

        var b = new SqlConnectionStringBuilder
        {
            DataSource = info.Server.Trim(),
            InitialCatalog = info.Database.Trim(),
            IntegratedSecurity = info.IntegratedSecurity,
            TrustServerCertificate = info.TrustServerCertificate,
            Encrypt = info.Encrypt,
        };

        if (!info.IntegratedSecurity)
        {
            b.UserID = info.UserId?.Trim() ?? string.Empty;
            b.Password = info.Password ?? string.Empty;
        }

        return b.ConnectionString;
    }
}
