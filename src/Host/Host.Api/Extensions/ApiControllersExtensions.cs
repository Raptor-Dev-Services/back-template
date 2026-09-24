using System.Text.Json.Serialization;
using Shared.Web;

namespace Host.Api.Extensions;

public static class ApiControllersExtensions
{
    /// <summary>
    /// Controllers con el contrato de la API: fechas siempre en UTC (query, ruta y cuerpo), enums por nombre
    /// en el JSON y un solo formato de error (ver <see cref="ApiErrorExtensions"/>).
    /// </summary>
    public static IMvcBuilder AddApiControllers(this IServiceCollection services) =>
        services
            .AddControllers(options =>
                // Primero: el binder de fechas del framework produce Kind=Unspecified y Npgsql lo rechaza.
                options.ModelBinderProviders.Insert(0, new UtcDateTimeModelBinderProvider()))
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeJsonConverter());
            })
            .AddApiErrorHandling();
}
