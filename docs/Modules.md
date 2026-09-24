# Modulos y capas

## Proyectos por modulo

Cada modulo son cinco proyectos. Las fronteras las impone el compilador (referencias de proyecto) y las vigila
`tests/Architecture.Tests/LayerBoundaryTests.cs`.

| Proyecto | Contiene | Puede referenciar |
|---|---|---|
| `{M}.Contracts` | DTOs, eventos de integracion, interfaces publicas (`I{M}Api`) | nada del modulo |
| `{M}.Domain` | entidades, interfaces de repositorio, reglas puras | `Shared.Kernel` |
| `{M}.Application` | casos de uso (Request/Handler/Responses), handlers de eventos | Domain, Contracts, `Shared.Kernel`, `Common.Messaging`, Contracts de otros modulos |
| `{M}.Infrastructure` | configuraciones EF, repositorios, servicios concretos | Domain, `Shared.Infrastructure` (**nunca** Application) |
| `{M}.Presentation` | controllers, presenters, cuerpos de peticion | Application, `Shared.Web` (**nunca** Infrastructure) |

`Host.Api` referencia Application + Infrastructure + Presentation de cada modulo y solo compone. Las pruebas viven
en `tests/`, no dentro del modulo.

**Reglas absolutas:** Application y Infrastructure no se referencian entre si; un modulo no referencia a otro salvo
por sus `Contracts`.

## Modulos actuales

| Modulo | Donde | Responsabilidad |
|---|---|---|
| Authentication | `src/Shared/Authentication/` | credenciales, sesiones (access + refresh con rotacion), RBAC, 2FA TOTP, bootstrap del primer tenant, invitaciones |
| Tenancy | `src/Modules/Tenancy/` | el `Tenant` (Active/Suspended), `ITenancyApi`, estado del tenant para el guard, y los casos de uso transversales: bitacora, tareas programadas, archivos |
| Users | `src/Modules/Users/` | `UserProfile` del tenant: listar, ver, editar, dar de baja |

Authentication vive en `src/Shared/` porque todos los demas dependen de su identidad; por dentro es un modulo
normal de cinco proyectos.

## Eventos entre modulos

| Evento | Lo publica | Lo consume |
|---|---|---|
| `UserShouldBeCreatedIntegrationEvent` (Authentication.Contracts) | bootstrap e invitacion | `Users.Application/IntegrationEventHandlers/UserShouldBeCreatedHandler` crea el perfil |
| `UserRegisteredIntegrationEvent` (Users.Contracts) | `UserShouldBeCreatedHandler` | nadie por ahora (punto de extension) |
| `UserDisabledIntegrationEvent` (Users.Contracts) | `DisableUserProfileHandler` | `Authentication.Application/IntegrationEventHandlers/UserDisabledHandler` apaga la credencial y revoca sesiones |

Los eventos se publican por el mediador **dentro** de la transaccion del caso de uso: o pasa todo o nada.

## Tablas de cada modulo

Un solo `AppDbContext` (Shared.Infrastructure). Cada modulo aporta sus configuraciones con
`services.AddModuleModel<UnaConfiguracionDelModulo>()` en su `Infrastructure/ServiceCollectionEx.cs`, y el
modulo se agrega a `AppDbContextFactory.Modules` (Host) para que `dotnet ef` vea sus tablas. Ver [DB.md](DB.md).

## Agregar un modulo

1. Crear los cinco proyectos bajo `src/Modules/{M}/` y agregarlos a `back-template.slnx`.
2. Referencias segun la tabla de arriba.
3. `Add{M}ApplicationServices`, `Add{M}InfrastructureServices` (con `AddModuleModel`), `Add{M}WebApiServices`
   (presenters + `AddControllers().AddApplicationPart(...)`), llamados desde `Program.cs`.
4. Marcador del modulo en `AppDbContextFactory.Modules`; migracion nueva.
5. Si tiene tablas con `TenantId`: su bloque en `001_enable_rls.sql`.
6. Pruebas: los guardias de arquitectura cargan los ensamblados por nombre; agrega el modulo a sus listas.
