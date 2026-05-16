using System.Text.Json.Serialization;

namespace Migrator.Core;

public sealed record SchemaTableDto(
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("name")] string Name)
{
    [JsonIgnore]
    public string QualifiedName => $"{Schema}.{Name}";
}

public sealed record SchemaColumnDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("dataType")] string DataType,
    [property: JsonPropertyName("isNullable")] bool IsNullable);

public sealed record MetadataTablesResponse(
    [property: JsonPropertyName("tables")] IReadOnlyList<SchemaTableDto> Tables);

public sealed record MetadataColumnsResponse(
    [property: JsonPropertyName("columns")] IReadOnlyList<SchemaColumnDto> Columns);

public sealed record MetadataTablesRequest(SqlServerConnectionInfo Connection);

public sealed record MetadataColumnsRequest(SqlServerConnectionInfo Connection, string Table);

public sealed record MysqlMetadataTablesRequest(MySqlConnectionInfo Connection);

public sealed record MysqlMetadataColumnsRequest(MySqlConnectionInfo Connection, string Table);

public sealed record OracleMetadataTablesRequest(OracleConnectionInfo Connection);

public sealed record OracleMetadataColumnsRequest(OracleConnectionInfo Connection, string Table);

public sealed record PostgresqlMetadataTablesRequest(PostgreSqlConnectionInfo Connection);

public sealed record PostgresqlMetadataColumnsRequest(PostgreSqlConnectionInfo Connection, string Table);
