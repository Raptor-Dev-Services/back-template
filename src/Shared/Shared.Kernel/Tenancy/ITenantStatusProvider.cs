namespace Shared.Kernel.Tenancy;

/// <summary>
/// "Este tenant puede operar ahora mismo?". Lo consulta el guard de tenant suspendido en CADA peticion
/// autenticada: el login y el refresh ya rechazan a un tenant suspendido, pero un access token emitido antes de
/// suspenderlo sigue siendo valido hasta que vence. El guard cierra esa ventana.
///
/// <para>La implementacion (modulo Tenancy) puede cachear unos segundos; suspender tarda a lo mucho eso en surtir
/// efecto, y a cambio no se paga una consulta por peticion.</para>
/// </summary>
public interface ITenantStatusProvider
{
    Task<bool> IsActiveAsync(long tenantId, CancellationToken cancellationToken = default);
}
