using System.Security.Cryptography;
using System.Text;
using Authentication.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using Shared.Kernel.Security;

namespace Authentication.Infrastructure.Services;

/// <summary>TOTP (RFC 6238) sobre Otp.NET: HMAC-SHA1, 6 digitos, periodo de 30 s, ventana de +-1 periodo.</summary>
public sealed class TotpService(TotpOptions options, ILogger<TotpService> logger) : ITotpService
{
    private static readonly VerificationWindow Window = new(previous: 1, future: 1);

    public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string BuildOtpauthUri(string secret, string accountLabel)
    {
        var issuer = Uri.EscapeDataString(options.Issuer);
        var label = Uri.EscapeDataString(accountLabel);
        return $"otpauth://totp/{issuer}:{label}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
    }

    public long? VerifyStep(string secret, string code, long minStepExclusive)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return null;

        Totp totp;
        try
        {
            totp = new Totp(Base32Encoding.ToBytes(secret));
        }
        catch (Exception ex)
        {
            // Un secreto ilegible y un codigo mal tecleado daban el mismo "codigo invalido" y son problemas
            // opuestos: el segundo se arregla reintentando, el primero nunca. Sin el secreto ni el codigo en el log.
            logger.LogWarning(ex, "Secreto TOTP ilegible (no es base32 valido).");
            return null;
        }

        if (!totp.VerifyTotp(code.Trim().Replace(" ", string.Empty), out var step, Window))
            return null;

        return step > minStepExclusive ? step : null;
    }
}

/// <summary>
/// AES-GCM (cifrado autenticado) para el secreto TOTP. La clave de 256 bits se deriva por SHA-256 de la clave de
/// configuracion. Salida: base64 de nonce (12) + tag (16) + ciphertext.
/// </summary>
public sealed class AesSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;
    private readonly ILogger<AesSecretProtector> _logger;

    public AesSecretProtector(TotpOptions options, ILogger<AesSecretProtector> logger)
    {
        if (string.IsNullOrWhiteSpace(options.EncryptionKey))
            throw new InvalidOperationException("Falta Totp:EncryptionKey (Totp__EncryptionKey): con ella se cifra el secreto 2FA.");

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(options.EncryptionKey));
        _logger = logger;
    }

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string? TryUnprotect(string ciphertext)
    {
        try
        {
            var combined = Convert.FromBase64String(ciphertext);
            var plain = new byte[combined.Length - NonceSize - TagSize];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(combined.AsSpan(0, NonceSize), combined.AsSpan(NonceSize + TagSize), combined.AsSpan(NonceSize, TagSize), plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            // Si aparece para TODOS los usuarios a la vez, alguien roto Totp:EncryptionKey: los secretos guardados
            // quedaron ilegibles y cada uno tiene que re-enrolar.
            _logger.LogWarning(ex, "No se pudo descifrar un secreto 2FA (clave rotada o dato manipulado).");
            return null;
        }
    }
}

/// <summary>Codigos <c>XXXX-XXXX</c> de un alfabeto sin caracteres ambiguos; en la base, SHA-256 del codigo normalizado.</summary>
public sealed class RecoveryCodes : IRecoveryCodes
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public IReadOnlyList<string> Generate(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => $"{Group()}-{Group()}")];

    public string Hash(string code) => SecureTokens.Hash(code.Trim().Replace("-", string.Empty).ToUpperInvariant());

    private static string Group() =>
        string.Create(4, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        });
}

/// <summary>
/// El reto del segundo paso: JWT de pocos minutos con AUDIENCIA propia (<c>{audiencia}-2fa</c>) y un claim de
/// alcance. Ningun esquema JwtBearer del Host acepta esa audiencia, asi que un reto robado no abre ningun
/// endpoint: solo sirve, junto con el codigo, en <c>/auth/login/2fa</c>.
/// </summary>
public sealed class TwoFactorChallenges(AuthOptions auth, TotpOptions options) : ITwoFactorChallenges
{
    private const string ScopeClaim = "scope";
    private const string ScopeValue = "2fa_pending";

    private string ChallengeAudience => auth.Audience + "-2fa";

    private SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(auth.Key));

    public string Issue(Guid credentialPublicId)
    {
        var nowUtc = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = auth.Issuer,
            Audience = ChallengeAudience,
            IssuedAt = nowUtc,
            NotBefore = nowUtc,
            Expires = nowUtc.AddMinutes(options.ChallengeMinutes),
            Claims = new Dictionary<string, object>
            {
                [AppClaimTypes.Subject] = credentialPublicId.ToString(),
                [ScopeClaim] = ScopeValue,
            },
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256),
        });
    }

    public async Task<Guid?> ValidateAsync(string challenge)
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(challenge, new TokenValidationParameters
        {
            ValidIssuer = auth.Issuer,
            ValidAudience = ChallengeAudience,
            IssuerSigningKey = Key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
        });

        if (!result.IsValid
            || !result.Claims.TryGetValue(ScopeClaim, out var scope) || scope as string != ScopeValue
            || !result.Claims.TryGetValue(AppClaimTypes.Subject, out var sub) || !Guid.TryParse(sub as string, out var id))
            return null;

        return id;
    }
}
