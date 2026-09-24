# Pruebas

```sh
dotnet build back-template.slnx -warnaserror   # 0 errores, 0 warnings
dotnet test back-template.slnx                 # necesita Docker (Testcontainers)
```

## Proyectos

| Proyecto | Que prueba | Como |
|---|---|---|
| `tests/Architecture.Tests` | fronteras de capa y de modulo, superficie de autorizacion, UTC, secretos en logs | NetArchTest + reflexion, carga de ensamblados por nombre (`RepoPaths`) |
| `tests/Authentication.Tests` | handlers de login, refresh y aprovisionamiento; primitivas de 2FA | unitarias, NSubstitute y fakes (`UseCases/Fakes.cs`) |
| `tests/Users.Tests` | handlers de Users | unitarias, NSubstitute |
| `tests/Shared.Tests` | filtro de excepciones, seleccion de correo, primitivas de archivos (firma de bytes, claves) | unitarias |
| `tests/Api.IntegrationTests` | la API real por HTTP contra un Postgres real | `WebApplicationFactory<Program>` + Testcontainers |

## Integracion

- `PostgresFixture`: un contenedor `postgres:17-alpine` por corrida; crea los roles `backtemplate_owner` y
  `backtemplate_app`, aplica grants, **migra con el rol dueno** y aplica RLS, igual que produccion. La API de prueba
  conecta con el rol de la aplicacion, asi que RLS y la falta de `DELETE` estan activos en las pruebas.
- `ApiFactory`: `Program.cs` completo en el entorno `Testing` con configuracion fija (`UseSetting`). Solo sustituye
  lo externo: el correo (`CapturingEmailSender`, del que las pruebas leen el enlace con token) y el almacenamiento
  de objetos (`InMemoryObjectStorage`). El despachador de tareas va apagado (`BackgroundJobs:DispatcherEnabled=false`);
  las tareas se ejercitan con "ejecutar ahora". Se puede crear una fabrica con overrides para un caso concreto.
- Helpers: `ApiClient` (envelope, `BootstrapTenantAsync`, `LoginAsync`), `TestJwt` (tokens firmados para un
  tenant/permiso), `Seed` (filas directas), `BoomController` (lanza para probar el envelope de 500).
- Todas las clases comparten la coleccion `postgres` (una base, en serie). Cada prueba crea sus propios tenants: no
  dependen del orden.

Areas cubiertas: convenciones del DbContext, aislamiento por RLS y por HTTP, deriva de la lista de exclusiones de
RLS, tenant suspendido, aprovisionamiento, sesiones, 2FA, bitacora, tareas programadas, archivos, envelope de
errores, borde HTTP, configuracion de produccion y salud.

## Estilo

- AAA, un comportamiento por prueba, nombres en espanol que dicen la regla (`La_clave_de_otro_tenant_responde_404...`).
- Se prueban los caminos de fallo y los bordes, no solo el feliz.
- Un arreglo se valida **quitandolo** y viendo caer su prueba (mutacion manual).
- Los handlers son `internal`; los proyectos de prueba los ven por `InternalsVisibleTo`.
