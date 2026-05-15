using Migrator.Core;
using Oracle.ManagedDataAccess.Client;

namespace Migrator.Infrastructure.Migration;

public static class OracleSchemaMetadataReader
{
    public static async Task<IReadOnlyList<SchemaTableDto>> GetTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var list = new List<SchemaTableDto>();
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT owner, table_name
            FROM all_tables
            WHERE owner NOT IN (
                'SYS', 'SYSTEM', 'XDB', 'CTXSYS', 'MDSYS', 'ORDSYS', 'OUTLN', 'WMSYS',
                'APPQOSSYS', 'DBSNMP', 'ORDDATA', 'AUDSYS', 'GSMADMIN_INTERNAL', 'OJVMSYS',
                'DVF', 'DVSYS', 'GGSYS', 'REMOTE_SCHEDULER_AGENT', 'SYS$UMF', 'SYSBACKUP',
                'SYSDG', 'SYSKM', 'SYSRAC')
              AND table_name NOT LIKE 'BIN$%'
              AND table_name NOT LIKE 'DR$%'
            ORDER BY owner, table_name
            """;

        await using var cmd = new OracleCommand(sql, conn) { CommandTimeout = 60 };
        await using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new SchemaTableDto(reader.GetString(0), reader.GetString(1)));
        }

        return list;
    }

    public static async Task<IReadOnlyList<SchemaColumnDto>> GetColumnsAsync(
        string connectionString,
        string qualifiedTable,
        CancellationToken cancellationToken = default)
    {
        OracleIdentifier.ParseTable(qualifiedTable, out var schema, out var table);
        var owner = schema.ToUpperInvariant();
        var name = table.ToUpperInvariant();

        var list = new List<SchemaColumnDto>();
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT column_name, data_type, nullable
            FROM all_tab_columns
            WHERE owner = :owner AND table_name = :name
            ORDER BY column_id
            """;

        await using var cmd = new OracleCommand(sql, conn) { CommandTimeout = 60, BindByName = true };
        cmd.Parameters.Add(new OracleParameter("owner", owner));
        cmd.Parameters.Add(new OracleParameter("name", name));

        await using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var colName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var nullable = string.Equals(reader.GetString(2), "Y", StringComparison.OrdinalIgnoreCase);
            list.Add(new SchemaColumnDto(colName, dataType, nullable));
        }

        return list;
    }
}
