using Migrator.Core;
using Npgsql;

namespace Migrator.Infrastructure.Migration;

public static class PostgreSqlConnectionStringFactory
{
    public static string Build(PostgreSqlConnectionInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (string.IsNullOrWhiteSpace(info.Server) || string.IsNullOrWhiteSpace(info.Database))
        {
            throw new InvalidOperationException("Servidor y base de datos son obligatorios para PostgreSQL.");
        }

        if (info.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("El puerto PostgreSQL debe estar entre 1 y 65535.");
        }

        var b = new NpgsqlConnectionStringBuilder
        {
            Host = info.Server.Trim(),
            Port = info.Port,
            Database = info.Database.Trim(),
            Username = (info.UserId ?? string.Empty).Trim(),
            Password = info.Password ?? string.Empty,
        };

        return b.ConnectionString;
    }
}
