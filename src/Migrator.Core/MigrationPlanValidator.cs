namespace Migrator.Core;

public sealed record MigrationPlanValidationResult(bool IsValid, IReadOnlyList<string> Errors);

public static class MigrationPlanValidator
{
    private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "insert",
        "upsert",
        "truncateReload",
    };

    public static MigrationPlanValidationResult ValidateStructure(MigrationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var errors = new List<string>();

        if (plan.Tables.Count == 0)
        {
            errors.Add("El plan no define ninguna tabla.");
        }

        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < plan.Tables.Count; i++)
        {
            var t = plan.Tables[i];
            var label = string.IsNullOrWhiteSpace(t.Source) ? $"índice {i}" : t.Source.Trim();

            if (string.IsNullOrWhiteSpace(t.Source))
            {
                errors.Add($"Tablas[{i}]: falta el nombre de tabla origen (Source).");
            }
            else if (!seenSources.Add(t.Source.Trim()))
            {
                errors.Add($"Tablas[{i}] ({label}): la tabla origen está duplicada en el plan.");
            }

            if (string.IsNullOrWhiteSpace(t.Target))
            {
                errors.Add($"Tablas[{i}] ({label}): falta el nombre de tabla destino (Target).");
            }

            if (string.IsNullOrWhiteSpace(t.Mode) || !AllowedModes.Contains(t.Mode.Trim()))
            {
                errors.Add($"Tablas[{i}] ({label}): modo no válido. Use insert, upsert o truncateReload.");
            }
            else if (IsUpsert(t.Mode) && !HasAnyNonEmptyKey(t.KeyColumns))
            {
                errors.Add($"Tablas[{i}] ({label}): en modo upsert debe indicarse al menos una columna en KeyColumns.");
            }

            if (t.Columns is not { Count: > 0 })
            {
                errors.Add($"Tablas[{i}] ({label}): no hay columnas mapeadas.");
            }
            else
            {
                var sourceCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var c = 0; c < t.Columns.Count; c++)
                {
                    var col = t.Columns[c];
                    if (string.IsNullOrWhiteSpace(col.Source))
                    {
                        errors.Add($"Tablas[{i}] ({label}), columna[{c}]: Source es obligatorio.");
                        continue;
                    }

                    var src = col.Source.Trim();
                    if (!sourceCols.Add(src))
                    {
                        errors.Add($"Tablas[{i}] ({label}): la columna origen '{src}' está duplicada.");
                    }
                }
            }
        }

        return new MigrationPlanValidationResult(errors.Count == 0, errors);
    }

    public static MigrationPlanValidationResult ValidateForExecution(MigrationPlan plan)
    {
        var structural = ValidateStructure(plan);
        if (!structural.IsValid)
        {
            return structural;
        }

        var errors = new List<string>();
        errors.AddRange(ValidateConnectionInfo(plan.Source, "Origen (Source)"));
        errors.AddRange(ValidateConnectionInfo(plan.Target, "Destino (Target)"));

        return errors.Count == 0
            ? structural
            : new MigrationPlanValidationResult(false, structural.Errors.Concat(errors).ToList());
    }

    private static IEnumerable<string> ValidateConnectionInfo(SqlServerConnectionInfo? info, string label)
    {
        if (info is null)
        {
            yield return $"{label}: no hay datos de conexión.";
            yield break;
        }

        if (string.IsNullOrWhiteSpace(info.Server))
        {
            yield return $"{label}: indique el servidor.";
        }

        if (string.IsNullOrWhiteSpace(info.Database))
        {
            yield return $"{label}: indique la base de datos.";
        }

        if (!info.IntegratedSecurity && string.IsNullOrWhiteSpace(info.UserId))
        {
            yield return $"{label}: con autenticación SQL indique el usuario.";
        }
    }

    private static bool IsUpsert(string mode) =>
        string.Equals(mode.Trim(), "upsert", StringComparison.OrdinalIgnoreCase);

    private static bool HasAnyNonEmptyKey(IReadOnlyList<string>? keys) =>
        keys is not null && keys.Any(static k => !string.IsNullOrWhiteSpace(k));
}
