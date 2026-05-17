using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(Guid userPublicId, string email, string role, long tenantId, long branchId)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(_config.GetValue<int>("Jwt:ExpirationMinutes", 60));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   userPublicId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role,               role),
            new Claim("tenant_id",                   tenantId.ToString()),
            new Claim("branch_id",                   branchId.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public DateTime GetRefreshTokenExpiry() =>
        DateTime.UtcNow.AddDays(_config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 30));
}
