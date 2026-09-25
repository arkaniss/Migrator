using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Migrator.Core;
using Migrator.Infrastructure.Data;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace Migrator.Infrastructure.Migration;

public sealed class SqlServerMigrationExecutor : IMigrationExecutor
{
    private const string StagingTableName = "#migr_staging";

    private enum SourceDialect
    {
        SqlServer,
        MySql,
        Oracle,
        PostgreSql,
    }

    public async Task<MigrationExecutionSummary> ExecuteAsync(
        MigrationPlan plan,
        MigrationExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        var validation = MigrationPlanValidator.ValidateForExecution(plan);
        if (!validation.IsValid)
        {
            return MigrationExecutionSummary.FromValidation(validation);
        }

        var targetCs = SqlServerConnectionStringFactory.Build(plan.Target);
        await using var targetCtx = new SessionDbContext(SessionDbContextFactory.CreateOptions(targetCs));
        await targetCtx.Database.OpenConnectionAsync(cancellationToken);
        var targetSql = (SqlConnection)targetCtx.Database.GetDbConnection();

        var dialect = plan.UsesOracleSource()
            ? SourceDialect.Oracle
            : plan.UsesPostgresqlSource()
                ? SourceDialect.PostgreSql
            : plan.UsesMySqlSource()
                ? SourceDialect.MySql
                : SourceDialect.SqlServer;
        var results = new List<TableMigrationResult>();
        var overallSuccess = true;

        if (plan.UsesOracleSource())
        {
            var oracleCs = OracleConnectionStringFactory.Build(plan.OracleSource!);
            await using var oracle = new OracleConnection(oracleCs);
            await oracle.OpenAsync(cancellationToken);

            foreach (var table in plan.Tables)
            {
                TableMigrationResult row;
                try
                {
                    row = await ProcessTableAsync(oracle, dialect, targetSql, table, options, cancellationToken);
                }
                catch (Exception ex)
                {
                    overallSuccess = false;
                    row = new TableMigrationResult(table.Source, table.Target, 0, 0, ex.Message);
                }

                results.Add(row);
                if (row.Error is not null)
                {
                    overallSuccess = false;
                    break;
                }
            }
        }
        else if (plan.UsesPostgresqlSource())
        {
            var pgCs = PostgreSqlConnectionStringFactory.Build(plan.PostgreSqlSource!);
            await using var pg = new NpgsqlConnection(pgCs);
            await pg.OpenAsync(cancellationToken);

            foreach (var table in plan.Tables)
            {
                TableMigrationResult row;
                try
                {
                    row = await ProcessTableAsync(pg, dialect, targetSql, table, options, cancellationToken);
                }
                catch (Exception ex)
                {
                    overallSuccess = false;
                    row = new TableMigrationResult(table.Source, table.Target, 0, 0, ex.Message);
                }

                results.Add(row);
                if (row.Error is not null)
                {
                    overallSuccess = false;
                    break;
                }
            }
        }
        else if (plan.UsesMySqlSource())
        {
            var mysqlCs = MySqlConnectionStringFactory.Build(plan.MySqlSource!);
            await using var mysql = new MySqlConnection(mysqlCs);
            await mysql.OpenAsync(cancellationToken);

            foreach (var table in plan.Tables)
            {
                TableMigrationResult row;
                try
                {
                    row = await ProcessTableAsync(mysql, dialect, targetSql, table, options, cancellationToken);
                }
                catch (Exception ex)
                {
                    overallSuccess = false;
                    row = new TableMigrationResult(table.Source, table.Target, 0, 0, ex.Message);
                }

                results.Add(row);
                if (row.Error is not null)
                {
                    overallSuccess = false;
                    break;
                }
            }
        }
        else
        {
            var sourceCs = SqlServerConnectionStringFactory.Build(plan.Source);
            await using var sourceCtx = new SessionDbContext(SessionDbContextFactory.CreateOptions(sourceCs));
            await sourceCtx.Database.OpenConnectionAsync(cancellationToken);
            var sourceSql = (SqlConnection)sourceCtx.Database.GetDbConnection();

            foreach (var table in plan.Tables)
            {
                TableMigrationResult row;
                try
                {
                    row = await ProcessTableAsync(sourceSql, dialect, targetSql, table, options, cancellationToken);
                }
                catch (Exception ex)
                {
                    overallSuccess = false;
                    row = new TableMigrationResult(table.Source, table.Target, 0, 0, ex.Message);
                }

                results.Add(row);
                if (row.Error is not null)
                {
                    overallSuccess = false;
                    break;
                }
            }
        }

        return MigrationExecutionSummary.Completed(results, overallSuccess);
    }

    private static async Task<TableMigrationResult> ProcessTableAsync(
        DbConnection sourceConn,
        SourceDialect sourceDialect,
        SqlConnection targetConn,
        TableMapping table,
        MigrationExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var sourceQualified = sourceDialect switch
        {
            SourceDialect.MySql => MySqlIdentifier.Qualify(table.Source),
            SourceDialect.Oracle => OracleIdentifier.Qualify(table.Source),
            SourceDialect.PostgreSql => PostgreSqlIdentifier.Qualify(table.Source),
            _ => SqlIdentifier.Qualify(table.Source),
        };
        var targetQualified = SqlIdentifier.Qualify(table.Target);

        var rowsRead = await CountRowsAsync(sourceConn, sourceDialect, sourceQualified, cancellationToken);

        if (options.DryRun)
        {
            return new TableMigrationResult(table.Source, table.Target, rowsRead, 0, null);
        }

        // DBCC CHECKIDENT (RESEED) es DDL: dentro de una transacción puede invalidarla si falla
        // y dejar el SqlTransaction en estado "completado" para el resto de comandos.
        if (table.ReseedToZero)
        {
            await TryReseedIdentityToZeroAsync(targetConn, table.Target, cancellationToken);
        }

        await using var tx = (SqlTransaction)await targetConn.BeginTransactionAsync(cancellationToken);

        try
        {
            if (IsTruncateReload(table.Mode))
            {
                await TryTruncateTargetAsync(targetConn, tx, targetQualified, cancellationToken);
            }

            if (IsUpsert(table.Mode))
            {
                await UpsertAsync(
                    sourceConn,
                    sourceDialect,
                    targetConn,
                    tx,
                    table,
                    sourceQualified,
                    targetQualified,
                    cancellationToken);
            }
            else
            {
                await BulkInsertAsync(
                    sourceConn,
                    sourceDialect,
                    targetConn,
                    tx,
                    table,
                    sourceQualified,
                    targetQualified,
                    cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return new TableMigrationResult(table.Source, table.Target, rowsRead, rowsRead, null);
        }
        catch
        {
            await TryRollbackAsync(tx, cancellationToken);
            throw;
        }
    }

    private static async Task TryRollbackAsync(SqlTransaction tx, CancellationToken cancellationToken)
    {
        try
        {
            await tx.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            /* transacción ya confirmada o invalidada (p. ej. error previo en la misma tx) */
        }
    }

    private static async Task<long> CountRowsAsync(
        DbConnection sourceConn,
        SourceDialect sourceDialect,
        string sourceQualified,
        CancellationToken cancellationToken)
    {
        var countExpr = sourceDialect is SourceDialect.MySql or SourceDialect.Oracle or SourceDialect.PostgreSql
            ? "COUNT(*)"
            : "COUNT_BIG(1)";
        var fromAndAlias = sourceDialect == SourceDialect.Oracle
            ? $" FROM {sourceQualified} s"
            : $" FROM {sourceQualified} AS s";
        var sql = $"SELECT {countExpr}{fromAndAlias}";
        await using var cmd = sourceConn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 0;
        var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task TryTruncateTargetAsync(
        SqlConnection targetConn,
        SqlTransaction tx,
        string targetQualified,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var truncate = new SqlCommand($"TRUNCATE TABLE {targetQualified};", targetConn, tx)
            {
                CommandTimeout = 0,
            };
            await truncate.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException)
        {
            await using var del = new SqlCommand($"DELETE FROM {targetQualified};", targetConn, tx)
            {
                CommandTimeout = 0,
            };
            await del.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task TryReseedIdentityToZeroAsync(
        SqlConnection targetConn,
        string targetQualifiedName,
        CancellationToken cancellationToken)
    {
        SqlIdentifier.ParseTable(targetQualifiedName, out var schema, out var table);

        const string hasIdentitySql = """
            SELECT TOP (1) 1
            FROM sys.identity_columns ic
            INNER JOIN sys.tables t ON t.object_id = ic.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = @schema AND t.name = @table;
            """;

        await using (var check = new SqlCommand(hasIdentitySql, targetConn) { CommandTimeout = 60 })
        {
            check.Parameters.AddWithValue("@schema", schema);
            check.Parameters.AddWithValue("@table", table);
            var has = await check.ExecuteScalarAsync(cancellationToken);
            if (has is null)
            {
                return;
            }
        }

        var qualified = SqlIdentifier.Qualify(targetQualifiedName);
        try
        {
            await using var cmd = new SqlCommand($"DBCC CHECKIDENT ({qualified}, RESEED, 1);", targetConn)
            {
                CommandTimeout = 60,
            };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException)
        {
            // Opción opcional: si falla (permisos, etc.), no bloqueamos la migración.
        }
    }

    private static async Task BulkInsertAsync(
        DbConnection sourceConn,
        SourceDialect sourceDialect,
        SqlConnection targetConn,
        SqlTransaction tx,
        TableMapping table,
        string sourceQualified,
        string targetQualified,
        CancellationToken cancellationToken)
    {
        var selectSql = BuildSelectSql(table, sourceQualified, sourceDialect);
        await using var reader = await ExecuteSourceReaderAsync(sourceConn, selectSql, cancellationToken);

        using var bulk = new SqlBulkCopy(targetConn, BulkCopyOptions(table), tx)
        {
            DestinationTableName = StripBracketsForBulkCopy(targetQualified),
            BulkCopyTimeout = 0,
            EnableStreaming = true,
        };

        foreach (var map in table.Columns)
        {
            var targetCol = ResolveTargetColumn(map);
            bulk.ColumnMappings.Add(targetCol, targetCol);
        }

        await bulk.WriteToServerAsync(reader, cancellationToken);
    }

    private static async Task<DbDataReader> ExecuteSourceReaderAsync(
        DbConnection sourceConn,
        string sql,
        CancellationToken cancellationToken)
    {
        var cmd = sourceConn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 0;
        return await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string StripBracketsForBulkCopy(string bracketedQualified)
    {
        var parts = bracketedQualified.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return bracketedQualified;
        }

        return $"{Unbracket(parts[0])}.{Unbracket(parts[1])}";
    }

    private static string Unbracket(string part) => part.Trim().TrimStart('[').TrimEnd(']').Replace("]]", "]", StringComparison.Ordinal);

    private static async Task UpsertAsync(
        DbConnection sourceConn,
        SourceDialect sourceDialect,
        SqlConnection targetConn,
        SqlTransaction tx,
        TableMapping table,
        string sourceQualified,
        string targetQualified,
        CancellationToken cancellationToken)
    {
        var targetKeys = ResolveTargetKeys(table);
        if (targetKeys.Count == 0)
        {
            throw new InvalidOperationException(
                "Modo upsert: KeyColumns debe indicar columnas de origen que existan en el mapeo (o nombres de columna destino).");
        }

        await DropStagingIfExistsAsync(targetConn, tx, cancellationToken);

        var createStaging = $@"
SELECT TOP (0) *
INTO {StagingTableName}
FROM {targetQualified};";

        await using (var create = new SqlCommand(createStaging, targetConn, tx) { CommandTimeout = 0 })
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var selectSql = BuildSelectSql(table, sourceQualified, sourceDialect);
        await using (var reader = await ExecuteSourceReaderAsync(sourceConn, selectSql, cancellationToken))
        {
            using var bulk = new SqlBulkCopy(targetConn, BulkCopyOptions(table), tx)
            {
                DestinationTableName = StagingTableName,
                BulkCopyTimeout = 0,
                EnableStreaming = true,
            };

            foreach (var map in table.Columns)
            {
                var targetCol = ResolveTargetColumn(map);
                bulk.ColumnMappings.Add(targetCol, targetCol);
            }

            await bulk.WriteToServerAsync(reader, cancellationToken);
        }

        var mergeSql = BuildMergeSql(table, targetQualified, targetKeys);
        await using var mergeCmd = new SqlCommand(mergeSql, targetConn, tx) { CommandTimeout = 0 };

        var identityCol = table.PreserveIdentityValues
            ? await GetMappedIdentityColumnNameAsync(targetConn, tx, table, cancellationToken)
            : null;

        if (identityCol is not null)
        {
            await using var onIdent = new SqlCommand(
                $"SET IDENTITY_INSERT {targetQualified} ON;",
                targetConn,
                tx)
            {
                CommandTimeout = 0,
            };
            await onIdent.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await mergeCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (identityCol is not null)
            {
                try
                {
                    await using var offIdent = new SqlCommand(
                        $"SET IDENTITY_INSERT {targetQualified} OFF;",
                        targetConn,
                        tx)
                    {
                        CommandTimeout = 0,
                    };
                    await offIdent.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqlException)
                {
                    /* si MERGE falló, la transacción puede estar abortada */
                }
                catch (InvalidOperationException)
                {
                    /* transacción ya no válida */
                }
            }
        }

        await DropStagingIfExistsAsync(targetConn, tx, cancellationToken);
    }

    private static async Task DropStagingIfExistsAsync(
        SqlConnection targetConn,
        SqlTransaction tx,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            IF OBJECT_ID('tempdb..{StagingTableName}') IS NOT NULL
                DROP TABLE {StagingTableName};
            """;
        await using var cmd = new SqlCommand(sql, targetConn, tx) { CommandTimeout = 0 };
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildSelectSql(TableMapping table, string sourceQualified, SourceDialect sourceDialect)
    {
        var sb = new StringBuilder();
        sb.Append("SELECT ");
        for (var i = 0; i < table.Columns.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var col = table.Columns[i];
            var tgt = ResolveTargetColumn(col);
            var fragment = GetSourceSelectFragment(col, sourceDialect);
            sb.Append(fragment).Append(" AS ").Append(FormatSelectAlias(tgt, sourceDialect));
        }

        sb.Append(" FROM ").Append(sourceQualified);
        sb.Append(sourceDialect == SourceDialect.Oracle ? " s" : " AS s");
        return sb.ToString();
    }

    private static string FormatSelectAlias(string columnName, SourceDialect sourceDialect) =>
        sourceDialect switch
        {
            SourceDialect.MySql => MySqlIdentifier.Backtick(columnName),
            SourceDialect.Oracle => OracleIdentifier.Quote(columnName),
            SourceDialect.PostgreSql => PostgreSqlIdentifier.Quote(columnName),
            _ => SqlIdentifier.Bracket(columnName),
        };

    private static string GetSourceSelectFragment(ColumnMapping col, SourceDialect sourceDialect)
    {
        if (!string.IsNullOrWhiteSpace(col.SourceExpression))
        {
            var err = SourceExpressionSyntax.Validate(col.SourceExpression);
            if (err is not null)
            {
                throw new InvalidOperationException(err);
            }

            return col.SourceExpression.Trim();
        }

        if (string.IsNullOrWhiteSpace(col.Source))
        {
            throw new InvalidOperationException(
                "ColumnMapping: falta Source y SourceExpression; se requiere al menos uno.");
        }

        var src = col.Source.Trim();
        return sourceDialect switch
        {
            SourceDialect.MySql => $"s.{MySqlIdentifier.Backtick(src)}",
            SourceDialect.Oracle => $"s.{OracleIdentifier.Quote(src)}",
            SourceDialect.PostgreSql => $"s.{PostgreSqlIdentifier.Quote(src)}",
            _ => $"s.{SqlIdentifier.Bracket(src)}",
        };
    }

    private static string BuildMergeSql(TableMapping table, string targetQualified, IReadOnlyList<string> targetKeys)
    {
        var keySet = new HashSet<string>(targetKeys, StringComparer.OrdinalIgnoreCase);
        var updateCols = table.Columns
            .Select(ResolveTargetColumn)
            .Where(c => !keySet.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var insertCols = table.Columns.Select(ResolveTargetColumn).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var on = string.Join(
            " AND ",
            targetKeys.Select(k => $"T.{SqlIdentifier.Bracket(k)} = S.{SqlIdentifier.Bracket(k)}"));

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"MERGE {targetQualified} AS T");
        sb.AppendLine($"USING {StagingTableName} AS S");
        sb.AppendLine($"ON ({on})");

        if (updateCols.Count > 0)
        {
            var sets = string.Join(
                ", ",
                updateCols.Select(c => $"T.{SqlIdentifier.Bracket(c)} = S.{SqlIdentifier.Bracket(c)}"));
            sb.AppendLine("WHEN MATCHED THEN");
            sb.AppendLine($"  UPDATE SET {sets}");
        }

        var insertColList = string.Join(", ", insertCols.Select(SqlIdentifier.Bracket));
        var insertValList = string.Join(", ", insertCols.Select(c => $"S.{SqlIdentifier.Bracket(c)}"));
        sb.AppendLine("WHEN NOT MATCHED THEN");
        sb.AppendLine($"  INSERT ({insertColList})");
        sb.AppendLine($"  VALUES ({insertValList});");

        return sb.ToString();
    }

    private static string ResolveTargetColumn(ColumnMapping map) =>
        string.IsNullOrWhiteSpace(map.Target) ? map.Source.Trim() : map.Target.Trim();

    private static IReadOnlyList<string> ResolveTargetKeys(TableMapping table)
    {
        var keys = new List<string>();
        foreach (var raw in table.KeyColumns)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var k = raw.Trim();
            var mapped = table.Columns.FirstOrDefault(c =>
                string.Equals(c.Source, k, StringComparison.OrdinalIgnoreCase));

            if (mapped is not null)
            {
                keys.Add(ResolveTargetColumn(mapped));
            }
            else
            {
                keys.Add(k);
            }
        }

        return keys;
    }

    private static bool IsUpsert(string? mode) =>
        !string.IsNullOrWhiteSpace(mode)
        && string.Equals(mode.Trim(), "upsert", StringComparison.OrdinalIgnoreCase);

    private static bool IsTruncateReload(string? mode) =>
        !string.IsNullOrWhiteSpace(mode)
        && string.Equals(mode.Trim(), "truncateReload", StringComparison.OrdinalIgnoreCase);

    private static SqlBulkCopyOptions BulkCopyOptions(TableMapping table)
    {
        var o = SqlBulkCopyOptions.TableLock;
        if (table.PreserveIdentityValues)
        {
            o |= SqlBulkCopyOptions.KeepIdentity;
        }

        return o;
    }

    /// <summary>
    /// Nombre de columna IDENTITY en destino si existe y está incluida en el mapeo (nombres destino).
    /// </summary>
    private static async Task<string?> GetMappedIdentityColumnNameAsync(
        SqlConnection targetConn,
        SqlTransaction tx,
        TableMapping table,
        CancellationToken cancellationToken)
    {
        SqlIdentifier.ParseTable(table.Target, out var schema, out var tableName);

        const string sql = """
            SELECT c.name
            FROM sys.identity_columns ic
            INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            INNER JOIN sys.tables t ON t.object_id = ic.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = @schema AND t.name = @table;
            """;

        string? identityName = null;
        await using (var cmd = new SqlCommand(sql, targetConn, tx) { CommandTimeout = 60 })
        {
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@table", tableName);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                identityName = r.GetString(0);
            }
        }

        if (identityName is null)
        {
            return null;
        }

        var mapped = table.Columns.Select(ResolveTargetColumn).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return mapped.Contains(identityName) ? identityName : null;
    }
}
