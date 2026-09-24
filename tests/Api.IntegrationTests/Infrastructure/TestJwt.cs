using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Security;

namespace Api.IntegrationTests.Infrastructure;

/// <summary>
/// Emite JWT de prueba firmados con la MISMA clave, emisor y audiencia con que <see cref="ApiFactory"/>
/// configura la API. Sirve para ejercitar autorizacion y aislamiento sin pasar por el login.
/// </summary>
public static class TestJwt
{
    public const string Key = "integration-tests-only-signing-key-0123456789abcdef";
    public const string Issuer = "back-template-tests";
    public const string Audience = "back-template-tests-clients";

    public static string Create(long tenantId, Guid? userId = null, IEnumerable<string>? permissions = null, IEnumerable<string>? roles = null)
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AppClaimTypes.Subject, (userId ?? Guid.NewGuid()).ToString()));
        identity.AddClaim(new Claim(AppClaimTypes.TenantId, tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        foreach (var role in roles ?? [])
            identity.AddClaim(new Claim(AppClaimTypes.Role, role));
        foreach (var permission in permissions ?? [])
            identity.AddClaim(new Claim(AppClaimTypes.Permission, permission));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
