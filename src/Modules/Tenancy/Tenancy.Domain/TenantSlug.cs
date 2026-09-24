using System.Text.RegularExpressions;

namespace Tenancy.Domain;

/// <summary>
/// Reglas del slug: identificador legible y estable de un tenant (aparece en URLs y en el login de soporte).
/// Minusculas, digitos y guiones, de 3 a 63 caracteres, sin guion al inicio ni al final.
/// </summary>
public static partial class TenantSlug
{
    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValid(string slug) => Pattern().IsMatch(slug);

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])$")]
    private static partial Regex Pattern();
}
