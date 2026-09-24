using System.Globalization;
using System.Security.Claims;
using System.Text;
using Authentication.Domain.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Security;

namespace Authentication.Infrastructure.Services;

/// <summary>
/// Emite el access JWT (HS256) con los claims de <see cref="AppClaimTypes"/>, firmado con la MISMA clave que
/// valida el Host. Los permisos viajan como claims <c>permission</c> multi-valor: la policy <c>perm:&lt;code&gt;</c>
/// los lee sin ir a la base en cada peticion. Por eso el access token es corto: un permiso retirado deja de
/// valer, como tarde, cuando vence.
/// </summary>
public sealed class AccessTokenIssuer(AuthOptions options) : IAccessTokenIssuer
{
    public AccessToken Issue(AccessTokenSubject subject)
    {
        var nowUtc = DateTime.UtcNow;
        var expiresAtUtc = nowUtc.AddMinutes(options.AccessTokenMinutes);

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AppClaimTypes.Subject, subject.UserId.ToString()));
        identity.AddClaim(new Claim(AppClaimTypes.TenantId, subject.TenantId.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(AppClaimTypes.Email, subject.Email));
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));
        foreach (var role in subject.Roles)
            identity.AddClaim(new Claim(AppClaimTypes.Role, role));
        foreach (var permission in subject.Permissions)
            identity.AddClaim(new Claim(AppClaimTypes.Permission, permission));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = nowUtc,
            NotBefore = nowUtc,
            Expires = expiresAtUtc,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expiresAtUtc);
    }
}
