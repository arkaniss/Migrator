using Microsoft.JSInterop;
using Migrator.Core;

namespace Migrator.Web.Services;

/// <summary>Confirmaciones al re-ejecutar tablas con progreso previo en el registro.</summary>
public static class MappingExecutionHelper
{
    public static string? BuildReExecutionWarning(
        RegistryMappingHost registry,
        IReadOnlyList<TableMapping> tables)
    {
        if (registry.ActiveWorkPlanId is null || tables.Count == 0)
        {
            return null;
        }

        var lines = new List<string>();
        foreach (var t in tables)
        {
            if (!registry.ProgressByPair.TryGetValue(
                    RegistryMappingHost.PairKey(t.Source, t.Target),
                    out var progress))
            {
                continue;
            }

            lines.Add(
                $"• {progress.SourceTable} → {progress.TargetTable} ({RegistryMappingHost.StatusLabel(progress.Status)})");
        }

        if (lines.Count == 0)
        {
            return null;
        }

        return
            "Advertencia: las siguientes tablas ya tienen registro en este trabajo y se volverán a ejecutar:\n\n"
            + string.Join("\n", lines);
    }

    public static async Task<bool> ConfirmExecutionAsync(
        IJSRuntime js,
        RegistryMappingHost registry,
        IReadOnlyList<TableMapping> tables,
        bool dryRun,
        string realExecutionPrompt)
    {
        var warning = BuildReExecutionWarning(registry, tables);
        if (dryRun)
        {
            if (warning is null)
            {
                return true;
            }

            return await js.InvokeAsync<bool>("confirm", warning + "\n\n¿Continuar con el dry-run?");
        }

        var message = warning is null
            ? realExecutionPrompt
            : warning + "\n\n" + realExecutionPrompt;
        return await js.InvokeAsync<bool>("confirm", message);
    }
}
