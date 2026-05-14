using Migrator.Core;
using MySqlConnector;

namespace Migrator.Infrastructure.Migration;

public static class MySqlConnectionStringFactory
{
    public static string Build(MySqlConnectionInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (string.IsNullOrWhiteSpace(info.Server) || string.IsNullOrWhiteSpace(info.Database))
        {
            throw new InvalidOperationException("Servidor y base de datos son obligatorios para MySQL.");
        }

        if (info.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("El puerto MySQL debe estar entre 1 y 65535.");
        }

        var b = new MySqlConnectionStringBuilder
        {
            Server = info.Server.Trim(),
            Port = (uint)info.Port,
            Database = info.Database.Trim(),
            UserID = (info.UserId ?? string.Empty).Trim(),
            Password = info.Password ?? string.Empty,
        };

        return b.ConnectionString;
    }
}
