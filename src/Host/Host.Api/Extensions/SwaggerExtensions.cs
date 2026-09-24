using Microsoft.OpenApi;

namespace Host.Api.Extensions;

public static class SwaggerExtensions
{
    /// <summary>
    /// OpenAPI con el esquema Bearer. Los modulos estan aislados y pueden repetir nombres de tipo (dos
    /// <c>CreateBody</c> en modulos distintos): el schemaId por defecto usa el nombre corto, colisiona y tumba el
    /// documento completo. El nombre completo del tipo los desambigua.
    /// </summary>
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = configuration["Swagger:Title"] ?? "back-template API",
                Version = "v1",
            });
            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Access token JWT (sin el prefijo Bearer).",
            });
            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer"), [] },
            });
        });

        return services;
    }

    /// <summary>
    /// Swagger solo en Development, o donde <c>Swagger:Enabled=true</c> lo pida explicitamente. En produccion el
    /// contrato completo de la API no se publica por omision.
    /// </summary>
    public static WebApplication UseSwaggerIfEnabled(this WebApplication app)
    {
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }
}
