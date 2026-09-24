using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Shared.Kernel.Security;

/// <summary>
/// Tokens opacos de un solo uso (refresh, invitacion, restablecer contrasena): el valor EN CLARO viaja una
/// sola vez al cliente o por correo, y en la base solo se guarda su hash. Quien lee la tabla -un respaldo, un
/// SQL de soporte- no obtiene ninguna sesion.
/// </summary>
public static class SecureTokens
{
    /// <summary>32 bytes de CSPRNG en base64url sin relleno (43 caracteres, seguros en URL).</summary>
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// SHA-256 en hex. Basta un hash rapido y sin sal porque el token ya tiene 256 bits de entropia: no hay
    /// diccionario que probar, a diferencia de una contrasena.
    /// </summary>
    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    /// <summary>Comparacion en tiempo constante (secretos de configuracion como el de bootstrap).</summary>
    public static bool FixedTimeEquals(string? provided, string expected) =>
        provided is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}
