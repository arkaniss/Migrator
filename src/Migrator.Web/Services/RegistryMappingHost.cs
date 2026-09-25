using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Migrator.Core;

namespace Migrator.Web.Services;

/// <summary>Estado y operaciones compartidas del registro de planes de trabajo.</summary>
public sealed class RegistryMappingHost
{
    private static readonly JsonSerializerOptions JsonRead = new() { PropertyNameCaseInsensitive = true };

    private readonly RegistryWorkPlanClient _client = new();
    private IHttpClientFactory? _httpFactory;
    private IJSRuntime? _js;
    private string _localStorageKey = string.Empty;
    private string _sourceKind = "sqlServer";

    public Guid? ActiveWorkPlanId { get; set; }
    public string WorkPlanName { get; set; } = string.Empty;
    public string? SourceConnectionId { get; set; }
    public string? TargetConnectionId { get; set; }

    public List<RegistryWorkPlanSummaryDto> WorkPlans { get; private set; } = new();
    public RegistryLastExecutedDto? LastExecuted { get; private set; }
    public IReadOnlyDictionary<string, RegistryTableProgressDto> ProgressByPair => _progressByPair;
    public IReadOnlyList<RegistryTableExecutionHistoryDto> ExecutionHistory => _executionHistory;

    private readonly Dictionary<string, RegistryTableProgressDto> _progressByPair = new(StringComparer.Ordinal);
    private List<RegistryTableExecutionHistoryDto> _executionHistory = new();

    private bool _sourceTablesLoaded;
    private bool _targetTablesLoaded;

    public Func<RegistryWorkPlanDetailDto, Task>? ApplyPlanDetail { get; set; }
    public Action<string?>? SetStatusMessage { get; set; }
    public Action? NotifyStateChanged { get; set; }

    public void Configure(
        IHttpClientFactory httpFactory,
        IJSRuntime js,
        string localStorageKey,
        string sourceKind)
    {
        _httpFactory = httpFactory;
        _js = js;
        _localStorageKey = localStorageKey;
        _sourceKind = sourceKind;
    }

    public bool SummaryMatchesPage(RegistryWorkPlanSummaryDto w)
    {
        var k = string.IsNullOrWhiteSpace(w.SourceKind) ? _sourceKind : w.SourceKind.Trim();
        return string.Equals(k, _sourceKind, StringComparison.OrdinalIgnoreCase);
    }

    public async Task LoadActiveIdFromStorageAsync()
    {
        if (_js is null)
        {
            return;
        }

        try
        {
            var regRaw = await _js.InvokeAsync<string?>("migratorLocalStorage.getItem", _localStorageKey);
            if (!string.IsNullOrWhiteSpace(regRaw) && Guid.TryParse(regRaw, out var regId))
            {
                ActiveWorkPlanId = regId;
            }
        }
        catch
        {
            /* */
        }
    }

    public void NotifySourceTablesLoaded(bool hasTables) => _sourceTablesLoaded = hasTables;

    public void NotifyTargetTablesLoaded(bool hasTables) => _targetTablesLoaded = hasTables;

    public async Task RefreshRemoteAsync()
    {
        if (_httpFactory is null)
        {
            return;
        }

        try
        {
            var http = _httpFactory.CreateClient("MigratorApi");
            var listResp = await http.GetAsync("api/migration/registry/work-plans");
            if (listResp.IsSuccessStatusCode)
            {
                WorkPlans = await listResp.Content.ReadFromJsonAsync<List<RegistryWorkPlanSummaryDto>>(JsonRead)
                            ?? new List<RegistryWorkPlanSummaryDto>();
            }

            LastExecuted = await http.GetFromJsonAsync<RegistryLastExecutedDto>(
                "api/migration/registry/last-executed",
                JsonRead);

            await NormalizeActiveForPageKindAsync();

            if (ActiveWorkPlanId is not null)
            {
                var meta = WorkPlans.FirstOrDefault(x => x.Id == ActiveWorkPlanId.Value);
                if (meta is not null)
                {
                    WorkPlanName = meta.Name;
                }
            }

            if (ActiveWorkPlanId is not null && WorkPlans.All(x => x.Id != ActiveWorkPlanId.Value))
            {
                await ClearActiveAsync();
            }
            else
            {
                await FetchProgressAndHistoryAsync();
            }

            NotifyStateChanged?.Invoke();
        }
        catch
        {
            /* registro no configurado */
        }
    }

    public async Task RefreshLastProgressAndHistoryAsync()
    {
        if (ActiveWorkPlanId is null || _httpFactory is null)
        {
            return;
        }

        try
        {
            var http = _httpFactory.CreateClient("MigratorApi");
            LastExecuted = await http.GetFromJsonAsync<RegistryLastExecutedDto>(
                "api/migration/registry/last-executed",
                JsonRead);
            await FetchProgressAndHistoryAsync();
            NotifyStateChanged?.Invoke();
        }
        catch
        {
            /* */
        }
    }

    public async Task<bool> EnsureActiveWorkPlanAsync(MigrationPlan plan)
    {
        if (_httpFactory is null || !RegistryWorkPlanClient.EndpointsConfigured(plan))
        {
            return false;
        }

        try
        {
            var http = _httpFactory.CreateClient("MigratorApi");
            if (ActiveWorkPlanId is null)
            {
                var match = await _client.MatchByEndpointsAsync(http, plan, _sourceKind);
                if (match?.Found == true && match.Summary is not null)
                {
                    ActiveWorkPlanId = match.Summary.Id;
                    WorkPlanName = match.Summary.Name;
                }
            }

            var saved = await _client.EnsureWorkPlanAsync(
                http,
                plan,
                _sourceKind,
                WorkPlanName,
                ActiveWorkPlanId,
                SourceConnectionId,
                TargetConnectionId);
            if (saved is null)
            {
                return false;
            }

            ActiveWorkPlanId = saved.Id;
            WorkPlanName = saved.Name;
            await PersistActiveIdAsync();
            await RefreshRemoteAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task TryRecordExecutionAsync(MigrationPlan plan, MigrationExecutionSummary summary, bool dryRun)
    {
        if (_httpFactory is null || !RegistryWorkPlanClient.EndpointsConfigured(plan))
        {
            return;
        }

        try
        {
            if (!await EnsureActiveWorkPlanAsync(plan))
            {
                return;
            }

            var http = _httpFactory.CreateClient("MigratorApi");
            await _client.RecordExecutionAsync(
                http,
                ActiveWorkPlanId!.Value,
                dryRun,
                summary,
                plan,
                SourceConnectionId,
                TargetConnectionId);
            await FetchProgressAndHistoryAsync();
            NotifyStateChanged?.Invoke();
        }
        catch
        {
            /* no bloquear migración */
        }
    }

    public async Task SavePlanAsync(MigrationPlan plan)
    {
        if (_httpFactory is null)
        {
            return;
        }

        if (plan.Tables.Count == 0)
        {
            SetStatusMessage?.Invoke("No hay tablas en el plan.");
            return;
        }

        SetStatusMessage?.Invoke(null);
        try
        {
            var http = _httpFactory.CreateClient("MigratorApi");
            var req = new SaveRegistryWorkPlanRequest
            {
                Id = ActiveWorkPlanId,
                MatchByEndpoints = ActiveWorkPlanId is null,
                Name = string.IsNullOrWhiteSpace(WorkPlanName)
                    ? "Plan " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                    : WorkPlanName.Trim(),
                Plan = MigrationPlanJson.Deserialize(MigrationPlanJson.Serialize(plan)),
                SourceConnectionId = SourceConnectionId,
                TargetConnectionId = TargetConnectionId,
            };
            var resp = await http.PostAsJsonAsync("api/migration/registry/work-plans", req);
            if (!resp.IsSuccessStatusCode)
            {
                SetStatusMessage?.Invoke(await resp.Content.ReadAsStringAsync());
                return;
            }

            var saved = await resp.Content.ReadFromJsonAsync<SaveRegistryWorkPlanResponse>(JsonRead);
            if (saved is null)
            {
                SetStatusMessage?.Invoke("Respuesta de registro no válida.");
                return;
            }

            ActiveWorkPlanId = saved.Id;
            WorkPlanName = saved.Name;
            await PersistActiveIdAsync();
            await RefreshRemoteAsync();
            SetStatusMessage?.Invoke($"Plan guardado en registro BD: {saved.Name} ({saved.Id})");
        }
        catch (Exception ex)
        {
            SetStatusMessage?.Invoke(ex.Message);
        }
    }

    public async Task LoadActivePlanAsync(MigrationPlan plan)
    {
        if (ActiveWorkPlanId is not Guid id || ApplyPlanDetail is null || _httpFactory is null)
        {
            return;
        }

        SetStatusMessage?.Invoke(null);
        try
        {
            var http = _httpFactory.CreateClient("MigratorApi");
            var resp = await http.GetAsync($"api/migration/registry/work-plans/{id:D}");
            if (!resp.IsSuccessStatusCode)
            {
                SetStatusMessage?.Invoke(await resp.Content.ReadAsStringAsync());
                return;
            }

            var detail = await resp.Content.ReadFromJsonAsync<RegistryWorkPlanDetailDto>(JsonRead);
            if (detail?.Plan is null || detail.Summary is null)
            {
                SetStatusMessage?.Invoke("Plan vacío en el registro.");
                return;
            }

            if (!SummaryMatchesPage(detail.Summary))
            {
                SetStatusMessage?.Invoke(
                    $"Este trabajo en BD es de tipo origen «{detail.Summary.SourceKind}». Cárgalo desde la página de mapeo correspondiente.");
                return;
            }

            ActiveWorkPlanId = id;
            WorkPlanName = detail.Summary.Name;
            SourceConnectionId = detail.Summary.SourceConnectionId ?? SourceConnectionId;
            TargetConnectionId = detail.Summary.TargetConnectionId ?? TargetConnectionId;
            await PersistActiveIdAsync();
            await ApplyPlanDetail(detail);
            await FetchProgressAndHistoryAsync();
            SetStatusMessage?.Invoke($"Plan cargado desde registro: {detail.Summary.Name}");
            NotifyStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            SetStatusMessage?.Invoke(ex.Message);
        }
    }

    public async Task UseLastExecutedPlanAsync()
    {
        await RefreshRemoteAsync();
        if (LastExecuted?.WorkPlanId is not Guid wid)
        {
            SetStatusMessage?.Invoke("No hay último plan ejecutado en el registro.");
            return;
        }

        var meta = WorkPlans.FirstOrDefault(x => x.Id == wid);
        if (meta is null || !SummaryMatchesPage(meta))
        {
            SetStatusMessage?.Invoke(
                "El último plan ejecutado en BD no pertenece a esta pantalla. Elige otro trabajo en la lista.");
            return;
        }

        ActiveWorkPlanId = wid;
        await PersistActiveIdAsync();
        if (ApplyPlanDetail is not null && _httpFactory is not null)
        {
            var http = _httpFactory.CreateClient("MigratorApi");
            var resp = await http.GetAsync($"api/migration/registry/work-plans/{wid:D}");
            if (resp.IsSuccessStatusCode)
            {
                var detail = await resp.Content.ReadFromJsonAsync<RegistryWorkPlanDetailDto>(JsonRead);
                if (detail is not null)
                {
                    await ApplyPlanDetail(detail);
                }
            }
        }

        await FetchProgressAndHistoryAsync();
        NotifyStateChanged?.Invoke();
    }

    public async Task OnSelectionChangedAsync(string? value)
    {
        if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out var id))
        {
            await ClearActiveAsync();
            NotifyStateChanged?.Invoke();
            return;
        }

        ActiveWorkPlanId = id;
        var meta = WorkPlans.FirstOrDefault(x => x.Id == id);
        if (meta is not null)
        {
            WorkPlanName = meta.Name;
        }

        await PersistActiveIdAsync();
        await FetchProgressAndHistoryAsync();
        NotifyStateChanged?.Invoke();
    }

    public async Task DeleteActiveAsync()
    {
        if (ActiveWorkPlanId is not Guid id || _httpFactory is null || _js is null)
        {
            return;
        }

        if (!await _js.InvokeAsync<bool>("confirm", "¿Eliminar este trabajo del registro en BD?"))
        {
            return;
        }

        try
        {
            var http = _httpFactory.CreateClient("MigratorApi");
            var resp = await http.DeleteAsync($"api/migration/registry/work-plans/{id:D}");
            await ClearActiveAsync();
            await RefreshRemoteAsync();
            SetStatusMessage?.Invoke(resp.IsSuccessStatusCode
                ? "Trabajo eliminado del registro."
                : await resp.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            SetStatusMessage?.Invoke(ex.Message);
        }
    }

    public string? CssForPair(string source, string target)
    {
        if (ActiveWorkPlanId is null)
        {
            return null;
        }

        return _progressByPair.TryGetValue(PairKey(source, target), out var dto)
            ? CssForStatus(dto.Status)
            : null;
    }

    public static string? CssForStatus(string? status) =>
        status switch
        {
            "completed" => "text-success",
            "failed" => "text-danger",
            "dryRunCompleted" => "text-info",
            "dryRunFailed" => "text-warning",
            _ => null,
        };

    public static string StatusLabel(string? status) =>
        status switch
        {
            "completed" => "Completada",
            "failed" => "Error",
            "dryRunCompleted" => "Dry-run OK",
            "dryRunFailed" => "Dry-run error",
            _ => status ?? "—",
        };

    public static string PairKey(string source, string target) => $"{source}\u001f{target}";

    public async Task TryResolveByEndpointsAsync(MigrationPlan plan)
    {
        if (!_sourceTablesLoaded || !_targetTablesLoaded || _httpFactory is null
            || !RegistryWorkPlanClient.EndpointsConfigured(plan))
        {
            return;
        }

        try
        {
            var http = _httpFactory.CreateClient("MigratorApi");
            var match = await _client.MatchByEndpointsAsync(http, plan, _sourceKind);
            if (match?.Found == true && match.Summary is not null)
            {
                ActiveWorkPlanId = match.Summary.Id;
                WorkPlanName = match.Summary.Name;
                SourceConnectionId = match.Summary.SourceConnectionId ?? SourceConnectionId;
                TargetConnectionId = match.Summary.TargetConnectionId ?? TargetConnectionId;
                await PersistActiveIdAsync();
                await FetchProgressAndHistoryAsync();
                SetStatusMessage?.Invoke(
                    $"Trabajo vinculado en BD: {match.Summary.Name}. El historial se conserva; usa «Cargar plan del trabajo activo» para restaurar mapeos.");
                NotifyStateChanged?.Invoke();
            }
        }
        catch
        {
            /* */
        }
    }

    private async Task FetchProgressAndHistoryAsync()
    {
        _progressByPair.Clear();
        _executionHistory = new List<RegistryTableExecutionHistoryDto>();
        if (ActiveWorkPlanId is null || _httpFactory is null)
        {
            return;
        }

        try
        {
            var http = _httpFactory.CreateClient("MigratorApi");
            var progressResp = await http.GetAsync(
                $"api/migration/registry/work-plans/{ActiveWorkPlanId.Value:N}/table-progress");
            if (progressResp.IsSuccessStatusCode)
            {
                var list = await progressResp.Content.ReadFromJsonAsync<List<RegistryTableProgressDto>>(JsonRead);
                if (list is not null)
                {
                    foreach (var p in list)
                    {
                        _progressByPair[PairKey(p.SourceTable, p.TargetTable)] = p;
                    }
                }
            }

            _executionHistory = await _client.FetchExecutionHistoryAsync(http, ActiveWorkPlanId.Value);
        }
        catch
        {
            /* */
        }
    }

    private async Task NormalizeActiveForPageKindAsync()
    {
        if (ActiveWorkPlanId is null)
        {
            return;
        }

        var meta = WorkPlans.FirstOrDefault(x => x.Id == ActiveWorkPlanId.Value);
        if (meta is null || !SummaryMatchesPage(meta))
        {
            await ClearActiveAsync();
        }
    }

    private async Task ClearActiveAsync()
    {
        ActiveWorkPlanId = null;
        WorkPlanName = string.Empty;
        _progressByPair.Clear();
        _executionHistory = new List<RegistryTableExecutionHistoryDto>();
        await PersistActiveIdAsync();
    }

    private async Task PersistActiveIdAsync()
    {
        if (_js is null)
        {
            return;
        }

        try
        {
            if (ActiveWorkPlanId is null)
            {
                await _js.InvokeVoidAsync("migratorLocalStorage.removeItem", _localStorageKey);
            }
            else
            {
                await _js.InvokeVoidAsync(
                    "migratorLocalStorage.setItem",
                    _localStorageKey,
                    ActiveWorkPlanId.Value.ToString("D"));
            }
        }
        catch
        {
            /* */
        }
    }
}
