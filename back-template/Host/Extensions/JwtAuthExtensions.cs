using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Host.Extensions;

public static class JwtAuthExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var key     = configuration["Jwt:Key"]      ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer  = configuration["Jwt:Issuer"]   ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateIssuer           = true,
                    ValidIssuer              = issuer,
                    ValidateAudience         = true,
                    ValidAudience            = audience,
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero
                };
            });

        return services;
    }
}
