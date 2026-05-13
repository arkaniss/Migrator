using System.Data;
using Microsoft.Data.SqlClient;
using Migrator.Core;

namespace Migrator.Infrastructure.Migration;

public static class SqlSchemaMetadataReader
{
    public static async Task<IReadOnlyList<SchemaTableDto>> GetTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var list = new List<SchemaTableDto>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME;
            """;

        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
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
        SqlIdentifier.ParseTable(qualifiedTable, out var schema, out var table);

        var list = new List<SchemaColumnDto>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @name
            ORDER BY ORDINAL_POSITION;
            """;

        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@name", table);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
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
