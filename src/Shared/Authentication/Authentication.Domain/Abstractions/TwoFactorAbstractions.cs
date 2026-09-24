namespace Authentication.Domain.Abstractions;

/// <summary>
/// El algoritmo TOTP (RFC 6238: HMAC-SHA1, 6 digitos, periodo de 30 s), sin estado. El estado 2FA vive en la
/// credencial; aqui solo el calculo, que es lo bastante delicado (base32, ventana, comparacion) para tener una
/// sola implementacion.
/// </summary>
public interface ITotpService
{
    /// <summary>Secreto nuevo de 160 bits en base32 (el formato que consumen las apps autenticadoras).</summary>
    string GenerateSecret();

    /// <summary>La URI <c>otpauth://totp/...</c> que el cliente pinta como QR.</summary>
    string BuildOtpauthUri(string secret, string accountLabel);

    /// <summary>
    /// Verifica el codigo con una ventana de +-1 periodo y devuelve el PASO que coincidio, o null si no verifica
    /// o si ese paso no supera <paramref name="minStepExclusive"/> (anti-replay).
    /// </summary>
    long? VerifyStep(string secret, string code, long minStepExclusive);
}

/// <summary>
/// Cifrado autenticado (AES-GCM) de secretos que deben guardarse de forma REVERSIBLE, como el secreto TOTP.
/// Un valor manipulado hace fallar el descifrado; nunca devuelve datos corruptos.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <summary>Descifra, o null si el valor no se puede descifrar (clave rotada, dato manipulado).</summary>
    string? TryUnprotect(string ciphertext);
}

/// <summary>Codigos de recuperacion: legibles al generarlos, y solo su hash para persistirlos.</summary>
public interface IRecoveryCodes
{
    IReadOnlyList<string> Generate(int count);

    /// <summary>Hash normalizado (sin guion y en mayusculas): el usuario puede teclearlo de cualquier forma.</summary>
    string Hash(string code);
}

/// <summary>
/// El reto del login en dos pasos: un token efimero con AUDIENCIA PROPIA (no valida como access token en ningun
/// endpoint) que identifica a la credencial que ya paso la contrasena y le falta el segundo factor.
/// </summary>
public interface ITwoFactorChallenges
{
    string Issue(Guid credentialPublicId);

    /// <summary>La credencial del reto, o null si es invalido, ajeno o expiro.</summary>
    Task<Guid?> ValidateAsync(string challenge);
}

/// <summary>Configuracion del 2FA (seccion <c>Totp</c>).</summary>
public sealed class TotpOptions
{
    public const string SectionName = "Totp";

    /// <summary>Etiqueta del emisor que muestra la app autenticadora.</summary>
    public string Issuer { get; set; } = "back-template";

    /// <summary>
    /// Clave con la que se cifra el secreto TOTP. Secreto, solo por entorno. Rotarla deja ilegibles los secretos
    /// guardados y obliga a re-enrolar: por eso es DISTINTA de Jwt:Key, que se rota por otras razones.
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;

    public int RecoveryCodeCount { get; set; } = 10;

    /// <summary>Vida del reto del segundo paso.</summary>
    public int ChallengeMinutes { get; set; } = 5;
}
