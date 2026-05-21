using Microsoft.EntityFrameworkCore;
using Migrator.Api;
using Migrator.Core;
using Migrator.Infrastructure.Data.Registry;
using Migrator.Infrastructure.Migration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Migrator API",
        Version = "v1",
        Description = "Validación y ejecución de migraciones, metadatos de tablas/columnas y registro de planes (SQL Server, MySQL, Oracle, PostgreSQL → SQL Server).",
    });
});
builder.Services.AddSingleton<IMigrationExecutor, SqlServerMigrationExecutor>();

var registryCs = builder.Configuration.GetConnectionString("MigrationRegistry");
if (!string.IsNullOrWhiteSpace(registryCs))
{
    builder.Services.AddDbContext<MigrationRegistryDbContext>(o =>
        o.UseSqlServer(registryCs, sql => sql.CommandTimeout(60)));
}
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

await InitializeMigrationRegistryAsync(app);

app.UseHttpsRedirection();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Migrator API v1");
        options.DocumentTitle = "Migrator API — Swagger";
        options.RoutePrefix = "swagger";
    });
}

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
        <li><a href="/swagger"><code>/swagger</code></a> — Swagger UI (solo desarrollo)</li>
        <li><a href="/swagger/v1/swagger.json"><code>GET /swagger/v1/swagger.json</code></a> — OpenAPI JSON</li>
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

app.MapPost("/api/migration/mysql/metadata/tables", async (
        MysqlMetadataTablesRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var cs = MySqlConnectionStringFactory.Build(request.Connection);
            var tables = await MySqlSchemaMetadataReader.GetTablesAsync(cs, cancellationToken);
            return Results.Json(new MetadataTablesResponse(tables));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("MysqlMetadataTables");

app.MapPost("/api/migration/mysql/metadata/columns", async (
        MysqlMetadataColumnsRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Table))
            {
                return Results.BadRequest(new { error = "Indique el nombre calificado de la tabla (p. ej. esquema.tabla)." });
            }

            var cs = MySqlConnectionStringFactory.Build(request.Connection);
            var columns = await MySqlSchemaMetadataReader.GetColumnsAsync(cs, request.Table.Trim(), cancellationToken);
            return Results.Json(new MetadataColumnsResponse(columns));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("MysqlMetadataColumns");

app.MapPost("/api/migration/postgresql/metadata/tables", async (
        PostgresqlMetadataTablesRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var cs = PostgreSqlConnectionStringFactory.Build(request.Connection);
            var tables = await PostgreSqlSchemaMetadataReader.GetTablesAsync(cs, cancellationToken);
            return Results.Json(new MetadataTablesResponse(tables));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("PostgresqlMetadataTables");

app.MapPost("/api/migration/postgresql/metadata/columns", async (
        PostgresqlMetadataColumnsRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Table))
            {
                return Results.BadRequest(new { error = "Indique el nombre calificado de la tabla (p. ej. esquema.tabla)." });
            }

            var cs = PostgreSqlConnectionStringFactory.Build(request.Connection);
            var columns = await PostgreSqlSchemaMetadataReader.GetColumnsAsync(cs, request.Table.Trim(), cancellationToken);
            return Results.Json(new MetadataColumnsResponse(columns));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("PostgresqlMetadataColumns");

app.MapPost("/api/migration/oracle/metadata/tables", async (
        OracleMetadataTablesRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var cs = OracleConnectionStringFactory.Build(request.Connection);
            var tables = await OracleSchemaMetadataReader.GetTablesAsync(cs, cancellationToken);
            return Results.Json(new MetadataTablesResponse(tables));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("OracleMetadataTables");

if (!string.IsNullOrWhiteSpace(registryCs))
{
    MigrationRegistryEndpoints.Map(app);
}

app.MapPost("/api/migration/oracle/metadata/columns", async (
        OracleMetadataColumnsRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Table))
            {
                return Results.BadRequest(new { error = "Indique el nombre calificado de la tabla (p. ej. esquema.tabla)." });
            }

            var cs = OracleConnectionStringFactory.Build(request.Connection);
            var columns = await OracleSchemaMetadataReader.GetColumnsAsync(cs, request.Table.Trim(), cancellationToken);
            return Results.Json(new MetadataColumnsResponse(columns));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("OracleMetadataColumns");

app.Run();

static async Task InitializeMigrationRegistryAsync(WebApplication app)
{
    var cs = app.Configuration.GetConnectionString("MigrationRegistry");
    if (string.IsNullOrWhiteSpace(cs))
    {
        return;
    }

    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<MigrationRegistryDbContext>();
        if (db is null)
        {
            return;
        }

        await db.Database.EnsureCreatedAsync();
        await MigrationRegistrySchemaRepair.RepairAppStateIdentityColumnAsync(db);
        await MigrationRegistrySchemaRepair.RepairWorkPlanAndHistorySchemaAsync(db);
        if (!await db.AppState.AnyAsync(x => x.Id == 1))
        {
            db.AppState.Add(new RegistryAppStateEntity { Id = 1 });
            await db.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MigrationRegistry");
        logger.LogWarning(
            ex,
            "No se pudo conectar o crear la base de registro (ConnectionStrings:MigrationRegistry). " +
            "La API sigue activa; los endpoints /api/migration/registry/* fallarán hasta corregir la cadena o arrancar SQL Server.");
    }
}

internal sealed record MigrationPlanValidationResponse(bool IsValid, IReadOnlyList<string> Errors);
