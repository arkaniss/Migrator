namespace Migrator.Core;

/// <summary>
/// Fusiona planes sin eliminar tablas ya registradas en el trabajo.
/// </summary>
public static class RegistryPlanMerger
{
    public static MigrationPlan MergeIntoStored(MigrationPlan stored, MigrationPlan incoming)
    {
        var merged = MigrationPlanJson.Deserialize(MigrationPlanJson.Serialize(stored));
        merged.SourceKind = incoming.SourceKind;
        merged.Source = CloneSql(incoming.Source);
        merged.Target = CloneSql(incoming.Target);
        merged.MySqlSource = incoming.MySqlSource is null ? null : CloneMySql(incoming.MySqlSource);
        merged.OracleSource = incoming.OracleSource is null ? null : CloneOracle(incoming.OracleSource);
        merged.PostgreSqlSource = incoming.PostgreSqlSource is null ? null : ClonePostgre(incoming.PostgreSqlSource);

        foreach (var table in incoming.Tables)
        {
            var idx = merged.Tables.FindIndex(t =>
                string.Equals(t.Source, table.Source, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Target, table.Target, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                merged.Tables[idx] = CloneTable(table);
            }
            else
            {
                merged.Tables.Add(CloneTable(table));
            }
        }

        return merged;
    }

    private static TableMapping CloneTable(TableMapping t) =>
        new()
        {
            Source = t.Source,
            Target = t.Target,
            Mode = t.Mode,
            ReseedToZero = t.ReseedToZero,
            PreserveIdentityValues = t.PreserveIdentityValues,
            KeyColumns = t.KeyColumns.ToList(),
            Columns = t.Columns.Select(c => new ColumnMapping
            {
                Source = c.Source,
                Target = c.Target,
                SourceExpression = c.SourceExpression,
            }).ToList(),
        };

    private static SqlServerConnectionInfo CloneSql(SqlServerConnectionInfo c) =>
        new()
        {
            Server = c.Server,
            Database = c.Database,
            IntegratedSecurity = c.IntegratedSecurity,
            UserId = c.UserId,
            Password = c.Password,
            TrustServerCertificate = c.TrustServerCertificate,
            Encrypt = c.Encrypt,
        };

    private static MySqlConnectionInfo CloneMySql(MySqlConnectionInfo c) =>
        new()
        {
            Server = c.Server,
            Port = c.Port,
            Database = c.Database,
            UserId = c.UserId,
            Password = c.Password,
        };

    private static OracleConnectionInfo CloneOracle(OracleConnectionInfo c) =>
        new()
        {
            Server = c.Server,
            Port = c.Port,
            ServiceName = c.ServiceName,
            UserId = c.UserId,
            Password = c.Password,
        };

    private static PostgreSqlConnectionInfo ClonePostgre(PostgreSqlConnectionInfo c) =>
        new()
        {
            Server = c.Server,
            Port = c.Port,
            Database = c.Database,
            UserId = c.UserId,
            Password = c.Password,
        };
}
