using Authentication.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Authentication.Infrastructure.Services;

/// <summary>BCrypt con work factor 12. Nunca se guarda ni se registra la contrasena en claro.</summary>
public sealed class PasswordHasher(ILogger<PasswordHasher> logger) : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <summary>
    /// Hash de un valor que nadie conoce, para gastar el MISMO tiempo cuando el correo no existe. Se calcula una
    /// vez por proceso (cuesta lo que un login).
    /// </summary>
    private static readonly Lazy<string> DummyHash = new(() => BCryptNet.HashPassword(Guid.NewGuid().ToString("N"), WorkFactor));

    public string Hash(string password) => BCryptNet.HashPassword(password, WorkFactor);

    public bool Verify(string password, string? passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash))
        {
            BCryptNet.Verify(password, DummyHash.Value);
            return false;
        }

        try
        {
            return BCryptNet.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException ex)
        {
            // Un hash corrupto y una contrasena mala se veian igual ("credenciales invalidas"), y son problemas
            // opuestos: el segundo lo arregla quien teclea; el primero significa que esa persona no podra entrar
            // NUNCA. El aviso lleva el hecho, no el hash ni la contrasena.
            logger.LogWarning(ex, "Hash de contrasena malformado: la credencial no se puede verificar.");
            return false;
        }
    }
}
