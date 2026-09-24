using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Shared.Web;

/// <summary>
/// Fuerza <see cref="DateTimeKind.Utc"/> en TODA fecha que entra por la API, venga del query string, de la
/// ruta o del cuerpo JSON.
///
/// <para><b>Por que existe.</b> Npgsql EXIGE <c>Kind=Utc</c> para un parametro <c>timestamptz</c>. El
/// enlazador de ASP.NET y <c>System.Text.Json</c> producen <c>Kind=Unspecified</c> cuando el texto no trae
/// zona (<c>?fromUtc=2026-07-01</c>), y la consulta revienta con un 500 cuyo mensaje no menciona la fecha.
/// Los clientes propios mandan <c>toISOString()</c> -con 'Z'- y por eso no se ve; cualquier otro llamador lo
/// encuentra al primer intento.</para>
///
/// <para><b>Por que en el limite.</b> El defecto no es de un caso de uso sino de la puerta por donde entra el
/// dato: arreglado aqui, lo hereda tambien el endpoint que nadie ha escrito todavia.</para>
///
/// <para><b>Etiqueta, no convierte.</b> El contrato de la API es UTC (los campos se llaman <c>*Utc</c>):
/// una fecha sin zona SE LEE como UTC. Una que trae offset explicito se convierte de verdad, porque ahi el
/// offset lo dice el cliente. La conversion a la zona del usuario es del frontend (regla utc-datetime).</para>
/// </summary>
public static class UtcDateTime
{
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

/// <summary>Aplica <see cref="UtcDateTime.Normalize"/> a las fechas del cuerpo JSON (y las escribe con 'Z').</summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UtcDateTime.Normalize(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(UtcDateTime.Normalize(value));
}

/// <summary>Gemelo del anterior para las fechas opcionales.</summary>
public sealed class UtcNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : UtcDateTime.Normalize(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(UtcDateTime.Normalize(value.Value));
    }
}

/// <summary>Enlaza las fechas del query string y de la ruta en UTC (esas no pasan por el deserializador JSON).</summary>
public sealed class UtcDateTimeModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);

        var text = value.FirstValue;
        var isOptional = Nullable.GetUnderlyingType(bindingContext.ModelType) is not null;
        if (string.IsNullOrWhiteSpace(text))
        {
            // Vacio con tipo opcional es "no vino"; con tipo obligatorio es un error de formato (400).
            if (isOptional) bindingContext.Result = ModelBindingResult.Success(null);
            else bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Fecha invalida.");
            return Task.CompletedTask;
        }

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Fecha invalida.");
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(UtcDateTime.Normalize(date));
        return Task.CompletedTask;
    }
}

/// <summary>Registra <see cref="UtcDateTimeModelBinder"/> para <c>DateTime</c> y <c>DateTime?</c>.</summary>
public sealed class UtcDateTimeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Metadata.UnderlyingOrModelType == typeof(DateTime) ? new UtcDateTimeModelBinder() : null;
    }
}
