using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Migrator.Core;
using Migrator.Infrastructure.Data.Registry;

namespace Migrator.Api;

internal static class MigrationRegistryEndpoints
{
    internal static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/migration/registry").WithTags("Migration registry");

        g.MapPost("/work-plans", async (
                SaveRegistryWorkPlanRequest req,
                MigrationRegistryDbContext db,
                CancellationToken ct) =>
            {
                var now = DateTimeOffset.UtcNow;
                var plan = req.Plan;
                var srcFp = RegistryEndpointFingerprint.OfSource(plan);
                var tgtFp = RegistryEndpointFingerprint.OfTarget(plan);

                RegistryWorkPlanEntity entity;
                if (req.Id is { } existingId && existingId != Guid.Empty)
                {
                    var found = await db.WorkPlans.FirstOrDefaultAsync(x => x.Id == existingId, ct);
                    if (found is null)
                    {
                        return Results.NotFound();
                    }

                    entity = found;
                }
                else if (req.MatchByEndpoints)
                {
                    var kind = (plan.SourceKind ?? "sqlServer").Trim();
                    entity = await FindByEndpointsAsync(db, kind, srcFp, tgtFp, ct)
                             ?? await FindByLegacyEndpointsAsync(
                                 db,
                                 kind,
                                 plan.Source.Server,
                                 plan.Source.Database,
                                 plan.Target.Server,
                                 plan.Target.Database,
                                 ct);
                    if (entity is null)
                    {
                        entity = new RegistryWorkPlanEntity
                        {
                            Id = Guid.NewGuid(),
                            Name = string.IsNullOrWhiteSpace(req.Name) ? "Sin nombre" : req.Name.Trim(),
                            CreatedAt = now,
                        };
                        db.WorkPlans.Add(entity);
                    }
                }
                else
                {
                    entity = new RegistryWorkPlanEntity
                    {
                        Id = Guid.NewGuid(),
                        Name = string.IsNullOrWhiteSpace(req.Name) ? "Sin nombre" : req.Name.Trim(),
                        CreatedAt = now,
                    };
                    db.WorkPlans.Add(entity);
                }

                if (!string.IsNullOrWhiteSpace(req.Name))
                {
                    entity.Name = req.Name.Trim();
                }

                MigrationRegistryMapper.ApplyEndpointFields(entity, plan);
                entity.SourceEndpointFingerprint = srcFp;
                entity.TargetEndpointFingerprint = tgtFp;
                if (!string.IsNullOrWhiteSpace(req.SourceConnectionId))
                {
                    entity.SourceConnectionId = req.SourceConnectionId.Trim();
                }

                if (!string.IsNullOrWhiteSpace(req.TargetConnectionId))
                {
                    entity.TargetConnectionId = req.TargetConnectionId.Trim();
                }

                entity.TableCount = plan.Tables.Count;
                entity.PlanJson = MigrationPlanJson.Serialize(plan);
                entity.UpdatedAt = now;
                if (entity.CreatedAt == default)
                {
                    entity.CreatedAt = now;
                }

                await db.SaveChangesAsync(ct);
                return Results.Json(new SaveRegistryWorkPlanResponse
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    UpdatedAt = entity.UpdatedAt,
                });
            })
            .WithName("RegistrySaveWorkPlan");

        g.MapGet("/work-plans/match", async (
                string sourceKind,
                string sourceEndpointFingerprint,
                string targetEndpointFingerprint,
                string? sourceServer,
                string? sourceDatabase,
                string? targetServer,
                string? targetDatabase,
                MigrationRegistryDbContext db,
                CancellationToken ct) =>
            {
                var kind = string.IsNullOrWhiteSpace(sourceKind) ? "sqlServer" : sourceKind.Trim();
                var entity = await FindByEndpointsAsync(
                    db,
                    kind,
                    sourceEndpointFingerprint.Trim(),
                    targetEndpointFingerprint.Trim(),
                    ct)
                    ?? await FindByLegacyEndpointsAsync(
                        db,
                        kind,
                        sourceServer,
                        sourceDatabase,
                        targetServer,
                        targetDatabase,
                        ct);
                if (entity is null)
                {
                    return Results.Json(new RegistryWorkPlanMatchDto { Found = false });
                }

                return Results.Json(new RegistryWorkPlanMatchDto
                {
                    Found = true,
                    Summary = MigrationRegistryMapper.ToSummaryDto(entity),
                });
            })
            .WithName("RegistryMatchWorkPlan");

        g.MapGet("/work-plans", async (MigrationRegistryDbContext db, CancellationToken ct) =>
            {
                var entities = await db.WorkPlans
                    .AsNoTracking()
                    .OrderByDescending(x => x.UpdatedAt)
                    .ToListAsync(ct);
                var list = entities.Select(MigrationRegistryMapper.ToSummaryDto).ToList();
                return Results.Json(list);
            })
            .WithName("RegistryListWorkPlans");

        g.MapGet("/work-plans/{id:guid}", async (Guid id, MigrationRegistryDbContext db, CancellationToken ct) =>
            {
                var e = await db.WorkPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null)
                {
                    return Results.NotFound();
                }

                var detail = new RegistryWorkPlanDetailDto
                {
                    Summary = MigrationRegistryMapper.ToSummaryDto(e),
                    Plan = MigrationPlanJson.Deserialize(e.PlanJson),
                };
                return Results.Json(detail);
            })
            .WithName("RegistryGetWorkPlan");

        g.MapDelete("/work-plans/{id:guid}", async (Guid id, MigrationRegistryDbContext db, CancellationToken ct) =>
            {
                var e = await db.WorkPlans.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (e is null)
                {
                    return Results.NotFound();
                }

                db.WorkPlans.Remove(e);
                var state = await db.AppState.FirstOrDefaultAsync(x => x.Id == 1, ct);
                if (state?.LastExecutedWorkPlanId == id)
                {
                    state.LastExecutedWorkPlanId = null;
                    state.LastExecutedAt = null;
                }

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithName("RegistryDeleteWorkPlan");

        g.MapGet("/work-plans/{id:guid}/table-progress", async (Guid id, MigrationRegistryDbContext db, CancellationToken ct) =>
            {
                if (!await db.WorkPlans.AsNoTracking().AnyAsync(x => x.Id == id, ct))
                {
                    return Results.NotFound();
                }

                var progressEntities = await db.TableProgress
                    .AsNoTracking()
                    .Where(x => x.WorkPlanId == id)
                    .OrderBy(x => x.SourceTable)
                    .ToListAsync(ct);
                var rows = progressEntities.Select(MigrationRegistryMapper.ToProgressDto).ToList();
                return Results.Json(rows);
            })
            .WithName("RegistryTableProgress");

        g.MapGet("/work-plans/{id:guid}/execution-history", async (
                Guid id,
                int? limit,
                MigrationRegistryDbContext db,
                CancellationToken ct) =>
            {
                if (!await db.WorkPlans.AsNoTracking().AnyAsync(x => x.Id == id, ct))
                {
                    return Results.NotFound();
                }

                var take = limit is > 0 and <= 5000 ? limit.Value : 500;
                var rows = await db.TableExecutionHistory
                    .AsNoTracking()
                    .Where(x => x.WorkPlanId == id)
                    .OrderByDescending(x => x.ExecutedAt)
                    .Take(take)
                    .ToListAsync(ct);
                return Results.Json(rows.Select(MigrationRegistryMapper.ToHistoryDto).ToList());
            })
            .WithName("RegistryExecutionHistory");

        g.MapGet("/last-executed", async (MigrationRegistryDbContext db, CancellationToken ct) =>
            {
                var state = await db.AppState.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
                if (state is null)
                {
                    return Results.Json(new RegistryLastExecutedDto(null, null));
                }

                return Results.Json(new RegistryLastExecutedDto(state.LastExecutedWorkPlanId, state.LastExecutedAt));
            })
            .WithName("RegistryLastExecuted");

        g.MapPost("/record-execution", async (
                RecordRegistryExecutionRequest body,
                MigrationRegistryDbContext db,
                CancellationToken ct) =>
            {
                var wp = await db.WorkPlans.FirstOrDefaultAsync(x => x.Id == body.WorkPlanId, ct);
                if (wp is null)
                {
                    return Results.NotFound();
                }

                var summary = body.Summary ?? new MigrationExecutionSummary(false, Array.Empty<string>(), Array.Empty<TableMigrationResult>());

                var now = DateTimeOffset.UtcNow;

                if (body.UpdatePlanSnapshot is not null)
                {
                    var stored = MigrationPlanJson.Deserialize(wp.PlanJson);
                    var merged = RegistryPlanMerger.MergeIntoStored(stored, body.UpdatePlanSnapshot);
                    MigrationRegistryMapper.ApplyEndpointFields(wp, merged);
                    wp.TableCount = merged.Tables.Count;
                    wp.PlanJson = MigrationPlanJson.Serialize(merged);
                    wp.UpdatedAt = now;
                }

                if (!string.IsNullOrWhiteSpace(body.SourceConnectionId))
                {
                    wp.SourceConnectionId = body.SourceConnectionId.Trim();
                }

                if (!string.IsNullOrWhiteSpace(body.TargetConnectionId))
                {
                    wp.TargetConnectionId = body.TargetConnectionId.Trim();
                }

                foreach (var row in summary.Tables ?? Array.Empty<TableMigrationResult>())
                {
                    var ok = row.Error is null;
                    var status = body.DryRun
                        ? ok ? "dryRunCompleted" : "dryRunFailed"
                        : ok ? "completed" : "failed";

                    string? mappingJson = null;
                    if (body.UpdatePlanSnapshot is not null)
                    {
                        var tm = body.UpdatePlanSnapshot.Tables.FirstOrDefault(t =>
                            string.Equals(t.Source, row.Source, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(t.Target, row.Target, StringComparison.OrdinalIgnoreCase));
                        if (tm is not null)
                        {
                            mappingJson = JsonSerializer.Serialize(tm, PlanPartJson.Options);
                        }
                    }

                    db.TableExecutionHistory.Add(new RegistryTableExecutionHistoryEntity
                    {
                        WorkPlanId = body.WorkPlanId,
                        SourceTable = row.Source,
                        TargetTable = row.Target,
                        DryRun = body.DryRun,
                        Status = status,
                        RowsRead = row.RowsRead,
                        RowsAffected = row.RowsAffected,
                        ExecutedAt = now,
                        ErrorMessage = row.Error,
                    });

                    var existing = await db.TableProgress.FirstOrDefaultAsync(
                        x => x.WorkPlanId == body.WorkPlanId
                             && x.SourceTable == row.Source
                             && x.TargetTable == row.Target,
                        ct);

                    if (existing is null)
                    {
                        db.TableProgress.Add(new RegistryTableProgressEntity
                        {
                            WorkPlanId = body.WorkPlanId,
                            SourceTable = row.Source,
                            TargetTable = row.Target,
                            Status = status,
                            RowsRead = row.RowsRead,
                            RowsAffected = row.RowsAffected,
                            LastAttemptAt = now,
                            LastSuccessAt = ok && !body.DryRun ? now : null,
                            ErrorMessage = row.Error,
                            TableMappingJson = mappingJson,
                        });
                    }
                    else
                    {
                        existing.Status = status;
                        existing.RowsRead = row.RowsRead;
                        existing.RowsAffected = row.RowsAffected;
                        existing.LastAttemptAt = now;
                        if (ok && !body.DryRun)
                        {
                            existing.LastSuccessAt = now;
                            existing.ErrorMessage = null;
                        }
                        else if (!ok)
                        {
                            existing.ErrorMessage = row.Error;
                        }
                        else if (body.DryRun && ok)
                        {
                            existing.ErrorMessage = null;
                        }

                        if (mappingJson is not null)
                        {
                            existing.TableMappingJson = mappingJson;
                        }
                    }
                }

                var appState = await db.AppState.FirstOrDefaultAsync(x => x.Id == 1, ct);
                if (appState is null)
                {
                    appState = new RegistryAppStateEntity { Id = 1 };
                    db.AppState.Add(appState);
                }

                appState.LastExecutedWorkPlanId = body.WorkPlanId;
                appState.LastExecutedAt = now;
                await db.SaveChangesAsync(ct);
                return Results.Ok();
            })
            .WithName("RegistryRecordExecution");
    }

    private static async Task<RegistryWorkPlanEntity?> FindByEndpointsAsync(
        MigrationRegistryDbContext db,
        string sourceKind,
        string sourceEndpointFingerprint,
        string targetEndpointFingerprint,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceEndpointFingerprint)
            || string.IsNullOrWhiteSpace(targetEndpointFingerprint))
        {
            return null;
        }

        return await db.WorkPlans
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(
                x => x.SourceKind == sourceKind
                     && x.SourceEndpointFingerprint == sourceEndpointFingerprint
                     && x.TargetEndpointFingerprint == targetEndpointFingerprint,
                ct);
    }

    private static async Task<RegistryWorkPlanEntity?> FindByLegacyEndpointsAsync(
        MigrationRegistryDbContext db,
        string sourceKind,
        string? sourceServer,
        string? sourceDatabase,
        string? targetServer,
        string? targetDatabase,
        CancellationToken ct)
    {
        var ss = sourceServer?.Trim() ?? string.Empty;
        var sd = sourceDatabase?.Trim() ?? string.Empty;
        var ts = targetServer?.Trim() ?? string.Empty;
        var td = targetDatabase?.Trim() ?? string.Empty;
        if (ss.Length == 0 || sd.Length == 0 || ts.Length == 0 || td.Length == 0)
        {
            return null;
        }

        return await db.WorkPlans
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(
                x => x.SourceKind == sourceKind
                     && x.SourceServer == ss
                     && x.SourceDatabase == sd
                     && x.TargetServer == ts
                     && x.TargetDatabase == td,
                ct);
    }

    private static class PlanPartJson
    {
        internal static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }
}
