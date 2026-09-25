using System.Text.Json;
using Migrator.Core;

namespace Migrator.Web.Services;

/// <summary>Resuelve y fusiona un <see cref="TableMapping"/> desde el progreso del registro hacia el plan en UI.</summary>
public static class RegistryProgressTableLoader
{
    private static readonly JsonSerializerOptions MappingJsonRead = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static TableMapping ResolveMapping(RegistryTableProgressDto progress, MigrationPlan plan)
    {
        if (progress.Mapping is not null)
        {
            return CloneTableMapping(progress.Mapping);
        }

        var fromPlan = plan.Tables.FirstOrDefault(t =>
            string.Equals(t.Source, progress.SourceTable, StringComparison.OrdinalIgnoreCase)
            && string.Equals(t.Target, progress.TargetTable, StringComparison.OrdinalIgnoreCase));
        if (fromPlan is not null)
        {
            return CloneTableMapping(fromPlan);
        }

        return new TableMapping
        {
            Source = progress.SourceTable,
            Target = progress.TargetTable,
            Mode = "insert",
        };
    }

    public static void MergeIntoPlan(TableMapping mapping, MigrationPlan plan)
    {
        var idx = plan.Tables.FindIndex(t =>
            string.Equals(t.Source, mapping.Source, StringComparison.OrdinalIgnoreCase)
            && string.Equals(t.Target, mapping.Target, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            plan.Tables[idx] = mapping;
        }
        else
        {
            plan.Tables.Add(mapping);
        }
    }

    public static TableMapping? TryDeserializeMappingJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TableMapping>(json, MappingJsonRead);
        }
        catch
        {
            return null;
        }
    }

    public static TableMapping CloneTableMapping(TableMapping t) =>
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
}
