using Migrator.Core;
using Oracle.ManagedDataAccess.Client;

namespace Migrator.Infrastructure.Migration;

public static class OracleConnectionStringFactory
{
    public static string Build(OracleConnectionInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Server))
        {
            throw new InvalidOperationException("Servidor es obligatorio para Oracle.");
        }

        if (info.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("El puerto Oracle debe estar entre 1 y 65535.");
        }

        if (string.IsNullOrWhiteSpace(info.ServiceName))
        {
            throw new InvalidOperationException("Nombre de servicio es obligatorio para Oracle (Easy Connect).");
        }

        if (string.IsNullOrWhiteSpace(info.UserId))
        {
            throw new InvalidOperationException("Usuario es obligatorio para Oracle.");
        }

        var port = info.Port is >= 1 and <= 65535 ? info.Port : 1521;
        var b = new OracleConnectionStringBuilder
        {
            DataSource = $"{info.Server.Trim()}:{port}/{info.ServiceName.Trim()}",
            UserID = info.UserId.Trim(),
            Password = info.Password ?? string.Empty,
        };

        return b.ConnectionString;
    }
}
