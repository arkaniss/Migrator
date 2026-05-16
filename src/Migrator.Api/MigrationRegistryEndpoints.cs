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
                var (kind, ss, sd, ts, td) = MigrationRegistryMapper.SummarizeEndpoints(req.Plan);
                var json = MigrationPlanJson.Serialize(req.Plan);

                RegistryWorkPlanEntity entity;
                if (req.Id is { } existingId && existingId != Guid.Empty)
                {
                    var found = await db.WorkPlans.FirstOrDefaultAsync(x => x.Id == existingId, ct);
                    if (found is null)
                    {
                        return Results.NotFound();
                    }

                    entity = found;
                    if (!string.IsNullOrWhiteSpace(req.Name))
                    {
                        entity.Name = req.Name.Trim();
                    }

                    entity.SourceKind = kind;
                    entity.SourceServer = ss;
                    entity.SourceDatabase = sd;
                    entity.TargetServer = ts;
                    entity.TargetDatabase = td;
                    entity.TableCount = req.Plan.Tables.Count;
                    entity.PlanJson = json;
                    entity.UpdatedAt = now;
                }
                else
                {
                    entity = new RegistryWorkPlanEntity
                    {
                        Id = Guid.NewGuid(),
                        Name = string.IsNullOrWhiteSpace(req.Name) ? "Sin nombre" : req.Name.Trim(),
                        SourceKind = kind,
                        SourceServer = ss,
                        SourceDatabase = sd,
                        TargetServer = ts,
                        TargetDatabase = td,
                        TableCount = req.Plan.Tables.Count,
                        PlanJson = json,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    db.WorkPlans.Add(entity);
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
                    var p = body.UpdatePlanSnapshot;
                    var (kind, ss, sd, ts, td) = MigrationRegistryMapper.SummarizeEndpoints(p);
                    wp.SourceKind = kind;
                    wp.SourceServer = ss;
                    wp.SourceDatabase = sd;
                    wp.TargetServer = ts;
                    wp.TargetDatabase = td;
                    wp.TableCount = p.Tables.Count;
                    wp.PlanJson = MigrationPlanJson.Serialize(p);
                    wp.UpdatedAt = now;
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
