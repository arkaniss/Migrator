using Migrator.Core;
using Npgsql;

namespace Migrator.Infrastructure.Migration;

public static class PostgreSqlSchemaMetadataReader
{
    public static async Task<IReadOnlyList<SchemaTableDto>> GetTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var list = new List<SchemaTableDto>();
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
              AND table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY table_schema, table_name;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 60 };
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
        PostgreSqlIdentifier.ParseTable(qualifiedTable, out var schema, out var table);

        var list = new List<SchemaColumnDto>();
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @name
            ORDER BY ordinal_position;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("name", table);

        await using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var colName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var nullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase);
            list.Add(new SchemaColumnDto(colName, dataType, nullable));
        }

        return list;
    }
}
