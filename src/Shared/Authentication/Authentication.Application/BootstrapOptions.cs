namespace Authentication.Application;

/// <summary>
/// Secreto que habilita el bootstrap de un tenant (crear el tenant y su PRIMER administrador sin sesion). Se
/// lee de <c>Bootstrap:Secret</c>, SOLO por entorno. Vacio = bootstrap deshabilitado (403), que es el estado
/// correcto de un despliegue una vez dado de alta su primer tenant.
/// </summary>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string? Secret { get; set; }
}
