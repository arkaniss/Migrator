using System.Text.RegularExpressions;

namespace Migrator.Infrastructure.Migration;

internal static partial class SqlIdentifier
{
    [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidPart();

    public static string Bracket(string identifier)
    {
        ValidatePart(identifier);
        return "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    public static void ParseTable(string qualifiedName, out string schema, out string table)
    {
        var parts = qualifiedName
            .Trim()
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        switch (parts.Length)
        {
            case 1:
                schema = "dbo";
                table = parts[0];
                break;
            case 2:
                schema = parts[0];
                table = parts[1];
                break;
            default:
                throw new InvalidOperationException(
                    $"Nombre de tabla no válido: '{qualifiedName}'. Use 'tabla' o 'esquema.tabla'.");
        }

        ValidatePart(schema);
        ValidatePart(table);
    }

    public static string Qualify(string qualifiedName)
    {
        ParseTable(qualifiedName, out var schema, out var table);
        return $"{Bracket(schema)}.{Bracket(table)}";
    }

    private static void ValidatePart(string part)
    {
        if (string.IsNullOrWhiteSpace(part) || !ValidPart().IsMatch(part))
        {
            throw new InvalidOperationException(
                $"Identificador SQL no permitido: '{part}'. Use solo letras, números y guion bajo; máximo convención esquema.tabla.");
        }
    }
}
