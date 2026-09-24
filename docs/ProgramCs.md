# Program.cs

`src/Host/Host.Api/Program.cs` solo compone: carga configuracion, registra servicios y arma el pipeline. La
logica de cada pieza vive en `Host.Api/Extensions/` y en los `ServiceCollectionEx` de cada modulo.

## Servicios, en orden

1. `LoadDotEnvInDevelopment()` antes del builder ([Config.md](Config.md)).
2. `ValidateScopes` + `ValidateOnBuild` en todos los entornos.
3. `EnsureProductionSettings`.
4. Logs (`AddAppLogging`, Serilog) y trazas/metricas (`AddObservability` de Common, OTLP).
5. Contexto: `ITenantContextAccessor` (singleton), `IHttpContextAccessor`, `ICurrentUser` = `HttpCurrentUser`.
6. `AddAppDatabase(connectionString)`: `AppDbContext`, interceptor de RLS, `IUnitOfWork`, `ITenantScope`, bitacora,
   `RlsRoleGuard`. **Sin migraciones al arrancar.**
7. Transversales: `AddAppEmail`, `AddBackgroundJobs`, `AddObjectStorage`, opciones de la purga de sesiones.
8. `AddMediator()` sin escaneo.
9. Por modulo: `Add{M}ApplicationServices`, `Add{M}InfrastructureServices`, `Add{M}WebApiServices` (Tenancy, Users,
   Authentication).
10. API: `AddApiControllers` (filtro de excepciones, binders UTC, respuesta de modelo invalido con envelope),
    `AddJwtAuthentication` (+ politicas `perm:*`), `AddHttpEdge` (CORS, ForwardedHeaders, limitador),
    `AddHealthServices`, Swagger.

## Pipeline: el orden es parte de la seguridad

```csharp
app.UseHttpEdge();          // ForwardedHeaders, correlacion, cabeceras de seguridad, HSTS
app.UseAppRequestLogging(); // un evento por peticion, sin query string
app.UseApiErrorHandling();  // envelope para todo error de aqui abajo
app.UseSwaggerIfEnabled();
app.UseCors(HttpEdgeExtensions.CorsPolicy);   // el preflight se resuelve sin token
app.UseAuthentication();
app.UseRateLimitingIfEnabled();               // despues de autenticar: particiona por sub
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();       // tenant del JWT ya validado
app.UseMiddleware<TenantStatusGuardMiddleware>();   // tenant suspendido -> 403
app.MapControllers();
app.MapHealth();            // /health/live y /health/ready, anonimos
```

- ForwardedHeaders va primero para que el limitador y los logs vean la IP real.
- El manejo de errores envuelve todo lo que sigue, asi un 401/429/500 sale con envelope y con `X-Correlation-Id`.
- El tenant se publica despues de autenticar; nunca se lee de la peticion.

`public partial class Program;` al final existe para `WebApplicationFactory<Program>` en las pruebas.
