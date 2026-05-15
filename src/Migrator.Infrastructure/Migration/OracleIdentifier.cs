using System.Text.RegularExpressions;

namespace Migrator.Infrastructure.Migration;

internal static partial class OracleIdentifier
{
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_$#]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidPart();

    public static string Quote(string identifier)
    {
        ValidatePart(identifier);
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public static void ParseTable(string qualifiedName, out string schema, out string table)
    {
        var parts = qualifiedName
            .Trim()
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        switch (parts.Length)
        {
            case 1:
                throw new InvalidOperationException(
                    $"Nombre de tabla Oracle no válido: '{qualifiedName}'. Use 'esquema.tabla' (p. ej. el valor devuelto al cargar tablas).");
            case 2:
                schema = parts[0];
                table = parts[1];
                break;
            default:
                throw new InvalidOperationException(
                    $"Nombre de tabla Oracle no válido: '{qualifiedName}'. Use 'esquema.tabla'.");
        }

        ValidatePart(schema);
        ValidatePart(table);
    }

    public static string Qualify(string qualifiedName)
    {
        ParseTable(qualifiedName, out var schema, out var table);
        return $"{Quote(schema)}.{Quote(table)}";
    }

    private static void ValidatePart(string part)
    {
        if (string.IsNullOrWhiteSpace(part) || !ValidPart().IsMatch(part))
        {
            throw new InvalidOperationException(
                $"Identificador Oracle no permitido: '{part}'. Use letras, números, _, $ o #; convención esquema.tabla.");
        }
    }
}
