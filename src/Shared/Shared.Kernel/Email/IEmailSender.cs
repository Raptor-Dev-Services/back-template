namespace Shared.Kernel.Email;

/// <summary>
/// Correo saliente transversal (invitaciones, restablecer contrasena, avisos). Abstraccion pura: la
/// implementacion (SMTP real, log de desarrollo o fallo ruidoso) vive en <c>Shared.Infrastructure</c> y la
/// elige el Host una sola vez, para todos los modulos.
///
/// <para>Una implementacion NUNCA registra el cuerpo en un entorno real: los enlaces llevan tokens de un
/// solo uso, y un token en el log es una cuenta tomada.</para>
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuracion del cliente web. <see cref="BaseUrl"/> es la base publica con la que se arman los enlaces
/// que se mandan por correo. En produccion es obligatoria (el Host no arranca sin ella): con el default, los
/// correos saldrian "bien" con enlaces a localhost y nadie se enteraria.
/// </summary>
public sealed class WebOptions
{
    public const string SectionName = "Web";

    public string BaseUrl { get; set; } = "http://localhost:5179";

    /// <summary>Enlace absoluto a una ruta del cliente web, con la query ya codificada.</summary>
    public string Link(string path, string queryKey, string queryValue) =>
        $"{BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}?{queryKey}={Uri.EscapeDataString(queryValue)}";
}
