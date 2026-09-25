using System.Globalization;

namespace Migrator.Core;

/// <summary>
/// Huella de endpoint para el registro de planes (sin usuario ni contraseña).
/// </summary>
public static class RegistryEndpointFingerprint
{
    public static string OfSource(MigrationPlan plan)
    {
        var kind = (plan.SourceKind ?? "sqlServer").Trim().ToLowerInvariant();
        if (plan.UsesPostgresqlSource() && plan.PostgreSqlSource is not null)
        {
            var c = plan.PostgreSqlSource;
            return $"postgresql|{(c.Server ?? "").Trim().ToLowerInvariant()}|{c.Port.ToString(CultureInfo.InvariantCulture)}|{(c.Database ?? "").Trim().ToLowerInvariant()}";
        }

        if (plan.UsesMySqlSource() && plan.MySqlSource is not null)
        {
            var c = plan.MySqlSource;
            return $"mysql|{(c.Server ?? "").Trim().ToLowerInvariant()}|{c.Port.ToString(CultureInfo.InvariantCulture)}|{(c.Database ?? "").Trim().ToLowerInvariant()}";
        }

        if (plan.UsesOracleSource() && plan.OracleSource is not null)
        {
            var c = plan.OracleSource;
            return $"oracle|{(c.Server ?? "").Trim().ToLowerInvariant()}|{c.Port.ToString(CultureInfo.InvariantCulture)}|{(c.ServiceName ?? "").Trim().ToLowerInvariant()}";
        }

        var s = plan.Source;
        var server = (s.Server ?? string.Empty).Trim().ToLowerInvariant();
        var db = (s.Database ?? string.Empty).Trim().ToLowerInvariant();
        return $"sql|{kind}|{server}|{db}|{s.IntegratedSecurity}";
    }

    public static string OfTarget(MigrationPlan plan)
    {
        var t = plan.Target;
        var server = (t.Server ?? string.Empty).Trim().ToLowerInvariant();
        var db = (t.Database ?? string.Empty).Trim().ToLowerInvariant();
        return $"sql|{server}|{db}|{t.IntegratedSecurity}";
    }
}
