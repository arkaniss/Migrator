namespace Migrator.Core;

/// <summary>
/// Reglas mínimas para fragmentos SQL embebidos en SELECT (origen). No sustituye revisión humana del SQL generado.
/// </summary>
public static class SourceExpressionSyntax
{
    public const int MaxLength = 4000;

    /// <summary>Devuelve mensaje de error en español, o null si es válido o vacío.</summary>
    public static string? Validate(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var e = expression.Trim();
        if (e.Length > MaxLength)
        {
            return $"La expresión no puede superar {MaxLength} caracteres.";
        }

        if (e.Contains(';', StringComparison.Ordinal))
        {
            return "No se permiten punto y coma (;) en la expresión.";
        }

        if (e.Contains("--", StringComparison.Ordinal))
        {
            return "No se permiten comentarios de línea (--) en la expresión.";
        }

        if (e.Contains("/*", StringComparison.Ordinal) || e.Contains("*/", StringComparison.Ordinal))
        {
            return "No se permiten comentarios /* */ en la expresión.";
        }

        return null;
    }
}
