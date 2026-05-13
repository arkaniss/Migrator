using Migrator.Core;
using Migrator.Infrastructure.Migration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IMigrationExecutor, SqlServerMigrationExecutor>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.SetIsOriginAllowed(_ => false);
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();

app.MapGet("/", () => Results.Content(
    """
    <!DOCTYPE html>
    <html lang="es">
    <head>
      <meta charset="utf-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1" />
      <title>Migrator API</title>
    </head>
    <body style="font-family: system-ui, sans-serif; max-width: 40rem; margin: 2rem auto; padding: 0 1rem;">
      <h1>Migrator API</h1>
      <p>La API está en marcha. No es una SPA: estas rutas sirven para comprobar que responde.</p>
      <ul>
        <li><a href="/health"><code>GET /health</code></a> — estado</li>
        <li><a href="/openapi/v1.json"><code>GET /openapi/v1.json</code></a> — documento OpenAPI (solo desarrollo)</li>
      </ul>
      <p style="color:#555;font-size:0.9rem;">El resto de operaciones son <code>POST</code> bajo <code>/api/migration/…</code> (usa .http, Postman o el Blazor WASM).</p>
    </body>
    </html>
    """,
    "text/html; charset=utf-8"));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health");

app.MapPost("/api/migration/validate-structure", (MigrationPlan plan) =>
    {
        var result = MigrationPlanValidator.ValidateStructure(plan);
        return Results.Json(new MigrationPlanValidationResponse(result.IsValid, result.Errors));
    })
    .WithName("ValidateMigrationPlanStructure");

app.MapPost("/api/migration/validate-execution", (MigrationPlan plan) =>
    {
        var result = MigrationPlanValidator.ValidateForExecution(plan);
        return Results.Json(new MigrationPlanValidationResponse(result.IsValid, result.Errors));
    })
    .WithName("ValidateMigrationPlanExecution");

app.MapPost("/api/migration/execute", async (
        MigrationPlan plan,
        bool dryRun,
        IMigrationExecutor executor,
        CancellationToken cancellationToken) =>
    {
        var summary = await executor.ExecuteAsync(
            plan,
            new MigrationExecutionOptions { DryRun = dryRun },
            cancellationToken);

        return Results.Json(summary);
    })
    .WithName("ExecuteMigration");

app.MapPost("/api/migration/metadata/tables", async (
        MetadataTablesRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var cs = SqlServerConnectionStringFactory.Build(request.Connection);
            var tables = await SqlSchemaMetadataReader.GetTablesAsync(cs, cancellationToken);
            return Results.Json(new MetadataTablesResponse(tables));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("MetadataTables");

app.MapPost("/api/migration/metadata/columns", async (
        MetadataColumnsRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Table))
            {
                return Results.BadRequest(new { error = "Indique el nombre calificado de la tabla (p. ej. dbo.Cliente)." });
            }

            var cs = SqlServerConnectionStringFactory.Build(request.Connection);
            var columns = await SqlSchemaMetadataReader.GetColumnsAsync(cs, request.Table.Trim(), cancellationToken);
            return Results.Json(new MetadataColumnsResponse(columns));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("MetadataColumns");

app.Run();

internal sealed record MigrationPlanValidationResponse(bool IsValid, IReadOnlyList<string> Errors);
