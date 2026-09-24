using System.Text;
using Authentication.Application;
using Authentication.Domain.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Security;
using Shared.Web.Authorization;

namespace Host.Api.Extensions;

public static class JwtAuthExtensions
{
    /// <summary>Largo minimo de la clave HMAC-SHA256: 32 bytes (256 bits). Menos que eso se puede adivinar.</summary>
    public const int MinKeyBytes = 32;

    /// <summary>
    /// Autenticacion JWT Bearer + autorizacion por permiso (<c>perm:&lt;code&gt;</c>), con fail-fast sobre la
    /// configuracion: sin clave, con una clave corta o con el placeholder de ejemplo, la API no arranca.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var auth = configuration.GetSection("Jwt").Get<AuthOptions>() ?? new AuthOptions();
        configuration.GetSection("Auth").Bind(auth);
        Validate(auth);
        services.AddSingleton(auth);

        var bootstrap = configuration.GetSection(BootstrapOptions.SectionName).Get<BootstrapOptions>() ?? new BootstrapOptions();
        if (!string.IsNullOrWhiteSpace(bootstrap.Secret) && Encoding.UTF8.GetByteCount(bootstrap.Secret) < MinKeyBytes)
            throw new InvalidOperationException(
                $"Bootstrap:Secret debe tener al menos {MinKeyBytes} bytes: protege un endpoint anonimo que crea administradores.");
        services.AddSingleton(bootstrap);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Los claims llegan con su nombre (sub, tenant_id, permission) y no traducidos a las URIs largas de
                // ClaimTypes: todo el sistema los lee con AppClaimTypes.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = auth.Issuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.Key)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    // Margen para relojes desalineados entre servidores; cero rechaza tokens validos, minutos
                    // extienden la vida de uno revocado.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AppClaimTypes.Email,
                    RoleClaimType = AppClaimTypes.Role,
                };
            });

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }

    private static void Validate(AuthOptions auth)
    {
        if (string.IsNullOrWhiteSpace(auth.Key))
            throw new InvalidOperationException("Falta Jwt:Key (Jwt__Key). Copia .env.example a .env o definela en el entorno.");
        if (Encoding.UTF8.GetByteCount(auth.Key) < MinKeyBytes)
            throw new InvalidOperationException($"Jwt:Key debe tener al menos {MinKeyBytes} bytes.");
        if (auth.Key.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) || auth.Key.Contains("change-me", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Jwt:Key todavia es el valor de ejemplo: genera una clave aleatoria (openssl rand -base64 48).");
        if (string.IsNullOrWhiteSpace(auth.Issuer) || string.IsNullOrWhiteSpace(auth.Audience))
            throw new InvalidOperationException("Faltan Jwt:Issuer y/o Jwt:Audience.");
        if (auth.AccessTokenMinutes is < 1 or > 1440 || auth.RefreshTokenDays is < 1 or > 90)
            throw new InvalidOperationException("Jwt:AccessTokenMinutes (1-1440) o Jwt:RefreshTokenDays (1-90) fuera de rango.");
    }
}
