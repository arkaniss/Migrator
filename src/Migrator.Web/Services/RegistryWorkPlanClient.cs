using System.Net.Http.Json;
using System.Text.Json;
using Migrator.Core;

namespace Migrator.Web.Services;

public sealed class RegistryWorkPlanClient
{
    private static readonly JsonSerializerOptions JsonRead = new() { PropertyNameCaseInsensitive = true };

    public static bool EndpointsConfigured(MigrationPlan plan)
    {
        if (!HasTarget(plan))
        {
            return false;
        }

        if (plan.UsesMySqlSource())
        {
            return plan.MySqlSource is not null
                   && !string.IsNullOrWhiteSpace(plan.MySqlSource.Server)
                   && !string.IsNullOrWhiteSpace(plan.MySqlSource.Database);
        }

        if (plan.UsesOracleSource())
        {
            return plan.OracleSource is not null
                   && !string.IsNullOrWhiteSpace(plan.OracleSource.Server)
                   && !string.IsNullOrWhiteSpace(plan.OracleSource.ServiceName);
        }

        if (plan.UsesPostgresqlSource())
        {
            return plan.PostgreSqlSource is not null
                   && !string.IsNullOrWhiteSpace(plan.PostgreSqlSource.Server)
                   && !string.IsNullOrWhiteSpace(plan.PostgreSqlSource.Database);
        }

        return !string.IsNullOrWhiteSpace(plan.Source.Server)
               && !string.IsNullOrWhiteSpace(plan.Source.Database);
    }

    public static (string SourceServer, string SourceDatabase) SourceEndpoint(MigrationPlan plan)
    {
        if (plan.UsesPostgresqlSource() && plan.PostgreSqlSource is not null)
        {
            return (plan.PostgreSqlSource.Server ?? "", plan.PostgreSqlSource.Database ?? "");
        }

        if (plan.UsesMySqlSource() && plan.MySqlSource is not null)
        {
            return (plan.MySqlSource.Server ?? "", plan.MySqlSource.Database ?? "");
        }

        if (plan.UsesOracleSource() && plan.OracleSource is not null)
        {
            return (plan.OracleSource.Server ?? "", plan.OracleSource.ServiceName ?? "");
        }

        return (plan.Source.Server ?? "", plan.Source.Database ?? "");
    }

    public static (string SourceFp, string TargetFp) EndpointFingerprints(MigrationPlan plan) =>
        (RegistryEndpointFingerprint.OfSource(plan), RegistryEndpointFingerprint.OfTarget(plan));

    public async Task<RegistryWorkPlanMatchDto?> MatchByEndpointsAsync(
        HttpClient http,
        MigrationPlan plan,
        string sourceKind,
        CancellationToken ct = default)
    {
        var (srcFp, tgtFp) = EndpointFingerprints(plan);
        var (srcServer, srcDb) = SourceEndpoint(plan);
        var url =
            $"api/migration/registry/work-plans/match?sourceKind={Uri.EscapeDataString(sourceKind)}"
            + $"&sourceEndpointFingerprint={Uri.EscapeDataString(srcFp)}"
            + $"&targetEndpointFingerprint={Uri.EscapeDataString(tgtFp)}"
            + $"&sourceServer={Uri.EscapeDataString(srcServer)}"
            + $"&sourceDatabase={Uri.EscapeDataString(srcDb)}"
            + $"&targetServer={Uri.EscapeDataString(plan.Target.Server ?? "")}"
            + $"&targetDatabase={Uri.EscapeDataString(plan.Target.Database ?? "")}";
        var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }

        return await resp.Content.ReadFromJsonAsync<RegistryWorkPlanMatchDto>(JsonRead, ct);
    }

    public async Task<List<RegistryTableExecutionHistoryDto>> FetchExecutionHistoryAsync(
        HttpClient http,
        Guid workPlanId,
        int limit = 200,
        CancellationToken ct = default)
    {
        var resp = await http.GetAsync(
            $"api/migration/registry/work-plans/{workPlanId:N}/execution-history?limit={limit}",
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            return new List<RegistryTableExecutionHistoryDto>();
        }

        return await resp.Content.ReadFromJsonAsync<List<RegistryTableExecutionHistoryDto>>(JsonRead, ct)
               ?? new List<RegistryTableExecutionHistoryDto>();
    }

    public async Task<SaveRegistryWorkPlanResponse?> EnsureWorkPlanAsync(
        HttpClient http,
        MigrationPlan plan,
        string sourceKind,
        string? name,
        Guid? existingId,
        string? sourceConnectionId,
        string? targetConnectionId,
        CancellationToken ct = default)
    {
        var req = new SaveRegistryWorkPlanRequest
        {
            Id = existingId,
            MatchByEndpoints = existingId is null,
            Name = string.IsNullOrWhiteSpace(name)
                ? $"Migración {DateTime.Now:yyyy-MM-dd HH:mm}"
                : name.Trim(),
            Plan = MigrationPlanJson.Deserialize(MigrationPlanJson.Serialize(plan)),
            SourceConnectionId = sourceConnectionId,
            TargetConnectionId = targetConnectionId,
        };
        var resp = await http.PostAsJsonAsync("api/migration/registry/work-plans", req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }

        return await resp.Content.ReadFromJsonAsync<SaveRegistryWorkPlanResponse>(JsonRead, ct);
    }

    public async Task RecordExecutionAsync(
        HttpClient http,
        Guid workPlanId,
        bool dryRun,
        MigrationExecutionSummary summary,
        MigrationPlan planSnapshot,
        string? sourceConnectionId,
        string? targetConnectionId,
        CancellationToken ct = default)
    {
        var req = new RecordRegistryExecutionRequest
        {
            WorkPlanId = workPlanId,
            DryRun = dryRun,
            Summary = summary,
            UpdatePlanSnapshot = MigrationPlanJson.Deserialize(MigrationPlanJson.Serialize(planSnapshot)),
            SourceConnectionId = sourceConnectionId,
            TargetConnectionId = targetConnectionId,
        };
        await http.PostAsJsonAsync("api/migration/registry/record-execution", req, ct);
    }

    private static bool HasTarget(MigrationPlan plan) =>
        !string.IsNullOrWhiteSpace(plan.Target.Server)
        && !string.IsNullOrWhiteSpace(plan.Target.Database);
}
