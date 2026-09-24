# Libreria Common

`Common/` es un **submodulo git** (`https://github.com/Raptor-Dev-Services/Common`) fijado a un commit concreto
(hoy `aedf830`, linea v2). Es **solo lectura** desde este repo: un cambio en Common se hace en su propio
repositorio, se publica y aqui solo se mueve el puntero del submodulo.

```sh
git submodule update --init          # despues de clonar
git -C Common log --oneline -1       # a que commit apunta
```

## Que se usa de Common

Common v2 son cinco sub-librerias; cada capa referencia solo lo que necesita (via `$(CommonRoot)`, definido en
`Directory.Build.props`):

| Ensamblado | Lo que usa esta plantilla | Quien lo referencia |
|---|---|---|
| `Common.Contracts` | `IResponse`, marcas `ISuccess<T>`/`INotFoundFailure`/`IConflictFailure`/`IValidationFailure`, `BusinessRuleException` | Domain/Application/Kernel |
| `Common.Messaging` | `IRequest`, `IRequestHandler`, `INotification(Handler)`, `IMediator` | Application, Presentation |
| `Common.Infra` | `AddMediator()` + `InteractorPipeline` (con enmascarado de secretos en el log), `AddObservability` (OpenTelemetry OTLP) | solo `Host.Api` |
| `Common.MultiTenancy` | `ITenantContextAccessor`, `TenantContext` | Shared.Infrastructure, Shared.Web |
| `Common.Web` | `ResultViewModel<T>` (el envelope) | Presentation, Shared.Web |

**No** se usan de Common: su registro de logging (la plantilla configura Serilog en
`ObservabilityExtensions`), el exportador Prometheus, sus migraciones por scripts al arranque ni sus factorias de
conexion por tenant. El acceso a datos es EF Core con un solo `AppDbContext`.

## Por que el mediador de Common

El pipeline publica el response del handler a su presenter: esa es la pieza que hace funcionar el patron
Request/Handler/Responses/Presenter. `AddMediator()` se invoca **sin ensamblados**: no escanea; cada modulo
registra sus handlers y presenters a mano. No se instala MediatR ni otro mediador (son incompatibles).

## Reglas de build

`Directory.Build.props` y `Directory.Packages.props` (CPM) aplican a la solucion **excepto** a los proyectos
bajo `Common/` (condicion `IsCommonSubmodule`): el submodulo compila con su propia configuracion.
