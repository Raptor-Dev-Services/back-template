using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Host.Extensions;

/// <summary>
/// Extensiones para configurar Swagger con soporte de autenticación JWT Bearer.
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>
    /// Registra Swagger con el esquema de seguridad Bearer para poder probar endpoints protegidos desde la UI.
    /// </summary>
    /// <param name="services">Colección de servicios del contenedor DI.</param>
    /// <returns>La misma <paramref name="services"/> para encadenamiento.</returns>
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(ConfigureSwagger);
        return services;
    }

    private static void ConfigureSwagger(SwaggerGenOptions options)
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name         = "Authorization",
            Type         = SecuritySchemeType.Http,
            Scheme       = "bearer",
            BearerFormat = "JWT",
            In           = ParameterLocation.Header,
            Description  = "Ingresa el token JWT sin el prefijo Bearer. Ejemplo: eyJhbGci..."
        });

        options.AddSecurityRequirement(_ =>
        {
            var requirement = new OpenApiSecurityRequirement();
            requirement.Add(new OpenApiSecuritySchemeReference("Bearer"), []);
            return requirement;
        });
    }
}
