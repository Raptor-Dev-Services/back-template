using Authentication.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Authentication.Infrastructure.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(Guid userPublicId, string email, string role, long tenantId)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var nowUtc = DateTime.UtcNow;

        var identity = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub,   userPublicId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role,               role),
            new Claim("tenant_id",                   tenantId.ToString()),
        ]);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject            = identity,
            Issuer             = _config["Jwt:Issuer"],
            Audience           = _config["Jwt:Audience"],
            IssuedAt           = nowUtc,
            NotBefore          = nowUtc,
            Expires            = nowUtc.AddMinutes(_config.GetValue<int>("Jwt:ExpirationMinutes", 60)),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public DateTime GetRefreshTokenExpiry() =>
        DateTime.UtcNow.AddDays(_config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 30));
}
