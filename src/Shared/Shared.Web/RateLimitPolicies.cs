namespace Shared.Web;

/// <summary>
/// Catalogo UNICO de nombres de politicas de limite de tasa. Los registra el Host y los citan los atributos
/// <c>[EnableRateLimiting(...)]</c>. El nombre se compara como TEXTO en tiempo de ejecucion: un atributo que
/// cita una politica que nadie registro no lo ve el compilador. Una prueba de arquitectura comprueba que ningun
/// atributo cite un nombre fuera de aqui.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Superficie de fuerza bruta: login, refresh, restablecer contrasena, bootstrap. Por IP y estricto.</summary>
    public const string Auth = "auth";

    /// <summary>Subida de archivos. Por USUARIO: rotar de IP no debe dar cuota nueva a quien ya porta un token.</summary>
    public const string Upload = "upload";
}
