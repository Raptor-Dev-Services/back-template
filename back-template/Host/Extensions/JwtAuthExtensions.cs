using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Host.Extensions;

/// <summary>
/// Extensiones para configurar la autenticación JWT Bearer.
/// </summary>
public static class JwtAuthExtensions
{
    /// <summary>
    /// Registra la autenticación JWT HS256 leyendo <c>Jwt:Key</c>, <c>Jwt:Issuer</c> y <c>Jwt:Audience</c> de la configuración.
    /// </summary>
    /// <param name="services">Colección de servicios del contenedor DI.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <returns>La misma <paramref name="services"/> para encadenamiento.</returns>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var key      = configuration["Jwt:Key"]       ?? throw new InvalidOperationException("Jwt:Key no está configurado.");
        var issuer   = configuration["Jwt:Issuer"]    ?? throw new InvalidOperationException("Jwt:Issuer no está configurado.");
        var audience = configuration["Jwt:Audience"]  ?? throw new InvalidOperationException("Jwt:Audience no está configurado.");

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
