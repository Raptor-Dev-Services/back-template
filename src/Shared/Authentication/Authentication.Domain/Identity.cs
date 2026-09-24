using System.Net.Mail;
using System.Text;

namespace Authentication.Domain;

/// <summary>
/// Criterio UNICO de normalizacion del correo, que es la llave del login. Lo teclea una persona -con la
/// mayuscula automatica del telefono, pegado desde otro correo con un espacio detras- y en Postgres la
/// comparacion de texto es exacta: sin esto, una cuenta valida responde "credenciales invalidas".
/// </summary>
public static class Identity
{
    public static string NormalizeEmail(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValidEmail(string email) =>
        email.Length is > 3 and <= 254
        && MailAddress.TryCreate(email, out var parsed)
        && string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Politica de contrasena, en un solo lugar para todos los casos de uso que fijan una (bootstrap,
/// invitacion, restablecimiento, cambio).
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    /// <summary>
    /// BCrypt solo usa los primeros 72 BYTES: dos contrasenas que compartan esos 72 serian la misma. Se
    /// rechaza lo que pase de ahi en vez de truncarlo en silencio.
    /// </summary>
    public const int MaxBytes = 72;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            return $"La contrasena debe tener al menos {MinLength} caracteres.";
        if (Encoding.UTF8.GetByteCount(password) > MaxBytes)
            return $"La contrasena no puede pasar de {MaxBytes} bytes.";
        return null;
    }
}
