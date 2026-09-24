namespace Host.Api.Extensions;

public static class AppConfigurationExtensions
{
    /// <summary>
    /// Carga el <c>.env</c> del repo SOLO en Development explicito, y sin pisar nada: una variable de entorno real
    /// siempre gana sobre el archivo (12-factor). Debe correr ANTES de <c>WebApplication.CreateBuilder</c> para
    /// que la configuracion vea las variables.
    ///
    /// <para>No en "cualquier cosa que no sea Production": las pruebas arrancan con el entorno Testing y fijan su
    /// propia configuracion; si el <c>.env</c> de quien las corre se colara, pisaria la base o la clave de prueba.</para>
    /// </summary>
    public static void LoadDotEnvInDevelopment()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            // TraversePath: sube por el arbol hasta encontrar el .env, se lance desde donde se lance.
            DotNetEnv.Env.TraversePath().NoClobber().Load();
        }
        catch (FileNotFoundException)
        {
            // Sin .env la configuracion llega por el entorno; los guards de arranque dicen que falta.
        }
    }

    /// <summary>
    /// Lo que en produccion no puede faltar aunque la API "arranque" sin ello. Cada uno de estos, ausente, deja un
    /// sistema que parece sano y falla despues, lejos de la causa:
    /// <list type="bullet">
    ///   <item><c>Web:BaseUrl</c>: sin ella los correos salen con enlaces a localhost; nadie recibe un error.</item>
    ///   <item><c>Cors:AllowedOrigins</c>: sin ella el navegador bloquea todo y el sintoma apunta a la API.</item>
    ///   <item><c>Smtp:Host</c>: sin ella cada invitacion y cada restablecimiento fallan al enviar.</item>
    ///   <item><c>ObjectStorage:AccessKey/SecretKey</c>: sin ellas cada subida de archivo falla.</item>
    /// </list>
    /// </summary>
    public static void EnsureProductionSettings(this IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;

        var missing = new List<string>();

        var baseUrl = configuration["Web:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl) || !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            missing.Add("Web__BaseUrl (https)");

        var cors = configuration.GetSection("Cors:AllowedOrigins");
        if (string.IsNullOrWhiteSpace(cors.Value) && !cors.GetChildren().Any())
            missing.Add("Cors__AllowedOrigins");

        if (string.IsNullOrWhiteSpace(configuration["Smtp:Host"]))
            missing.Add("Smtp__Host");

        if (string.IsNullOrWhiteSpace(configuration["ObjectStorage:AccessKey"]) || string.IsNullOrWhiteSpace(configuration["ObjectStorage:SecretKey"]))
            missing.Add("ObjectStorage__AccessKey / ObjectStorage__SecretKey");

        if (missing.Count > 0)
            throw new InvalidOperationException(
                "Configuracion de produccion incompleta: " + string.Join(", ", missing) + ". Ver .env.example y docs/deploy/.");
    }
}
