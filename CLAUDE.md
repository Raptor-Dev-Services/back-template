# CLAUDE.md - back-template

Reglas para trabajar en este repo (personas y agentes). El detalle de cada tema vive en `docs/`; esto es lo que
no se negocia y donde va cada cosa. Si el codigo y este archivo discrepan, gana el codigo y se corrige este.

## Arquitectura

Monolito modular. Cada modulo son 5 proyectos: `Contracts` (POCO publicos), `Domain`, `Application`,
`Infrastructure`, `Presentation`. `Host.Api` compone. Direccion de dependencias:

```
Domain          -> Shared.Kernel
Contracts       -> nada
Application     -> Domain + Contracts (+ Contracts de otros modulos) + Shared.Kernel + Common.Messaging
Infrastructure  -> Domain + Shared.Infrastructure        (NUNCA Application)
Presentation    -> Application + Shared.Web              (NUNCA Infrastructure)
Host.Api        -> Application + Infrastructure + Presentation de cada modulo
```

Un modulo nunca referencia a otro salvo por su `Contracts`. `Architecture.Tests` lo hace cumplir por ensamblado.

## El patron de caso de uso (obligatorio)

`Application/UseCases/<Accion>/`: `<Accion>Request` (`IRequest<TResponse>`), `<Accion>Handler`
(`internal sealed`), `Responses/<Accion>Response` (record abstracto + `Success` con `ISuccess<T>` + fallos
tipados). En `Presentation`: presenter que hereda de `ResultPresenter<TController, TResponse, TData>` y el
controller que deriva de `BaseApiController` y hace `MapResult(await DispatchAsync(request, ct), viewModel)`.

- **Registro MANUAL**, sin escaneo: el handler en `<Modulo>.Application/ServiceCollectionEx.cs`, el presenter
  en `<Modulo>.Presentation/ServiceCollectionEx.cs`. Sin registro, el endpoint falla.
- Mediador de `Common.Messaging`. **Nunca** MediatR.
- El controller no tiene logica ni `try/catch`. Un fallo esperado es un `Response` tipado; una regla de negocio
  rota puede lanzar una `BusinessException` de `Shared.Kernel.Errors`; lo inesperado sale como 500 generico y
  su detalle va al log, **nunca** al cliente.

Mapeo unico (`Shared.Web/Errors/FailureStatusCodes.cs`): `IValidationFailure` y `IBadRequestFailure` 400 (ADR-0007),
`IUnauthorizedFailure` 401, `IForbiddenFailure` 403, `INotFoundFailure` 404, `IConflictFailure` 409.

## Datos

- Un solo `AppDbContext` (`Shared.Infrastructure`); cada modulo aporta su modelo con `AddModuleModel<T>()` y
  su marcador en `AppDbContextFactory.Modules`.
- Entidades de negocio heredan de `TenantEntity` (TenantId, auditoria, soft delete, `xmin` como concurrencia).
  `SaveChanges` sella tenant y actor; `Remove` es soft delete; el rol de la app no tiene DELETE.
- **Tenant solo del claim `tenant_id`**, nunca del body, query ni headers. `IgnoreQueryFilters` solo en
  autenticacion y tareas de plataforma, y siempre con nombre (`[QueryFilterNames.Tenant]`).
- Toda tabla nueva con `TenantId` lleva su bloque en `Persistence/Sql/001_enable_rls.sql`, o entra a la lista de
  excluidas (con motivo) en `RlsIsolationTests.ExcludedOnPurpose` **y** en `deploy.yml`. Una prueba compara ambas.
- Migraciones: `dotnet ef migrations add <Nombre> -p src/Shared/Shared.Infrastructure -s src/Host/Host.Api
  -o Persistence/Migrations`, y se aplican con `./scripts/dev-db.sh migrate` (rol dueno). La API nunca migra.
- Fechas: UTC siempre (`timestamptz`); una fecha sin zona no llega a la base.
- SQL crudo solo parametrizado (`FromSqlInterpolated`), nunca concatenado.

## Seguridad

- Todo endpoint declara `[Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.X)]` o esta en la lista
  justificada de `AuthorizationSurfaceTests`. Un permiso nuevo va en `KnownPermissions` y en `RbacCatalog`.
- Secretos solo por variable de entorno (`.env` en local, GitHub Secrets en produccion). Nada en `appsettings*`.
- Nada de contrasenas, tokens ni codigos en logs, bitacora ni respuestas.

## Reglas de trabajo

1. `Common/` es un submodulo fijado a un commit: **no se edita** desde aqui.
2. Antes de commitear: `dotnet build back-template.slnx -warnaserror` (0/0) y `dotnet test back-template.slnx`.
3. Un arreglo se valida quitandolo y viendo caer su prueba.
4. Commits en Conventional Commits, en espanol. Tags solo si una persona lo pide; al crear uno, actualizar `TAGS.md`.
5. Docs en espanol; identificadores en ingles.
6. Base de datos local: solo la base `backtemplate` del devstack. Nunca tocar la de otro producto.

## Donde esta cada cosa

| Tema | Codigo | Doc |
|---|---|---|
| Errores y envelope | `Shared.Web/Errors`, `Shared.Kernel/Errors` | `docs/Errors.md` |
| Tenancy y RLS | `Shared.Infrastructure/Persistence`, `Shared.Web/Tenancy` | `docs/MultiTenancy.md` |
| Auth, RBAC, 2FA | `src/Shared/Authentication` | `docs/Auth.md` |
| Tareas programadas | `Shared.Kernel/BackgroundJobs`, `Shared.Infrastructure/BackgroundJobs` | ADR 0004 |
| Archivos | `Shared.Kernel/Storage`, `Shared.Infrastructure/Storage` | ADR 0005 |
| Entorno local | `scripts/dev-db.sh`, `compose-dev.yaml` | `docs/DEV-STACK.md` |
| Entrega | `Dockerfile`, `.github/workflows`, `docker-compose.prod.yml` | `docs/deploy/` |
