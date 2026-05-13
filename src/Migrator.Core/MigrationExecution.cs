using System.Text.Json.Serialization;

namespace Migrator.Core;

public interface IMigrationExecutor
{
    Task<MigrationExecutionSummary> ExecuteAsync(
        MigrationPlan plan,
        MigrationExecutionOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class MigrationExecutionOptions
{
    public bool DryRun { get; init; }
}

public sealed record TableMigrationResult(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("rowsRead")] long RowsRead,
    [property: JsonPropertyName("rowsAffected")] long RowsAffected,
    [property: JsonPropertyName("error")] string? Error);

public sealed record MigrationExecutionSummary(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("globalErrors")] IReadOnlyList<string>? GlobalErrors,
    [property: JsonPropertyName("tables")] IReadOnlyList<TableMigrationResult>? Tables)
{
    public static MigrationExecutionSummary FromValidation(MigrationPlanValidationResult validation) =>
        new(false, validation.Errors.ToList(), Array.Empty<TableMigrationResult>());

    public static MigrationExecutionSummary Completed(IReadOnlyList<TableMigrationResult> tables, bool success) =>
        new(success, Array.Empty<string>(), tables);
}
