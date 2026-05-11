# CLAUDE.md

## Propósito

Guía de referencia para Claude Code al trabajar en este repositorio (`back-template`).

Este documento describe la arquitectura, convenciones y reglas que deben respetarse en todo cambio, generación o revisión de código.

---

## Arquitectura

**Patrón:** Clean Architecture + CQRS + Mediator + Presenter

**Capas y dirección de dependencias:**

```
Domain
Application    → Domain
Infrastructure → Domain + Common
WebApi         → Application + Common
Host           → Application + Infrastructure + WebApi + Common
Common         (transversal, sin lógica de negocio del proyecto)
```

**Regla absoluta:** las dependencias apuntan hacia adentro. `Application` nunca referencia `Infrastructure`. `WebApi` nunca accede a PostgreSQL directamente.

---

## Árbol de directorios

```
back-template/
├── Domain/
│   ├── Entities/{Modulo}/
│   └── Repositories/{Modulo}/
├── Application/
│   ├── Dto/{Modulo}/
│   ├── UseCases/
│   │   └── {Modulo}/{Accion}/
│   │       ├── {Accion}Request.cs
│   │       ├── {Accion}Handler.cs
│   │       └── Responses/
│   │           ├── {Accion}Response.cs      ← abstract record : IResponse
│   │           ├── {Accion}Success.cs       ← sealed record : {Accion}Response, ISuccess<T>
│   │           └── {Accion}Failure.cs       ← sealed record : {Accion}Response, INotFoundFailure (etc)
│   └── ServiceCollectionEx.cs
├── Infrastructure/
│   ├── PostgreSql/
│   │   ├── MainDbConnection.cs              ← clase marcadora (clave ConnectionStrings)
│   │   ├── MainDbConnectionFactory.cs       ← abre NpgsqlConnections
│   │   └── MainDapperDbConnection.cs        ← ÚNICO punto de ejecución SQL
│   ├── Persistence/SQLDB/Main/{Modulo}/{Entidad}Sql.cs
│   ├── Repositories/{Modulo}/
│   └── ServiceCollectionEx.cs
├── WebApi/
│   ├── Base/BaseApiController.cs
│   ├── EndPoints/{Modulo}/
│   │   ├── {Modulo}Controller.cs
│   │   ├── Presenters/{Accion}Presenter.cs
│   │   └── RequestBodies/{Accion}Body.cs
│   └── ServiceCollectionEx.cs
├── Host/
│   ├── Program.cs
│   ├── Extensions/
│   ├── appsettings.json
│   └── Services/Schema Migration/Tables/*.sql
├── Tests/
└── Common/   (submódulo — NO editar desde este repo)
```

---

## Stack tecnológico

| Categoría | Tecnología |
|-----------|-----------|
| Runtime | .NET 10 / C# 12 |
| Framework | ASP.NET Core 10 |
| Base de datos | PostgreSQL 17 |
| ORM | Dapper (raw SQL parametrizado) |
| Driver | Npgsql 8 |
| Mediator | Custom — `Common.Messaging` (NO MediatR NuGet) |
| Auth | JWT Bearer HS256 |
| Passwords | BCrypt.Net-Next |
| Logging | Serilog → Seq |
| Tracing | OpenTelemetry OTLP → Jaeger |
| Métricas | Prometheus en `/metrics` |
| Health | `/api/health` |
| Testing | xUnit |
| Deploy | Docker multi-stage |

---

## REGLA DE ORO — Acceso a datos: `MainDapperDbConnection`

**Todo SQL del proyecto pasa obligatoriamente por `MainDapperDbConnection`.**

### Cadena completa

```
ConnectionStrings:MainDbConnection (appsettings.json)
    ↓
MainDbConnectionFactory  (abre NpgsqlConnection)
    ↓
MainDapperDbConnection   (ejecuta Dapper + logs de performance automáticos)
    ↓
{Entidad}Sql classes     (inyectan MainDapperDbConnection)
```

### Clases `...Sql`

Cada tabla tiene una clase `{Entidad}Sql` bajo `Infrastructure/Persistence/SQLDB/Main/{Modulo}/`:

- Recibe `MainDapperDbConnection` por constructor.
- Agrupa **todos** los queries de esa tabla — ninguno fuera de ella.
- SQL como raw strings `"""..."""`. Nunca concatenación ni interpolación.
- Parámetros siempre como objeto anónimo `new { param }`.
- Retorna entidades de dominio directamente — sin Row classes intermedias.

```csharp
public sealed class ExampleUsersSql
{
    private readonly MainDapperDbConnection _db;
    public ExampleUsersSql(MainDapperDbConnection db) => _db = db;

    public Task<ExampleUser?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        _db.QuerySingleAsync<ExampleUser>(
            """
            SELECT Id, PublicId, FullName, Email, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.ExampleUsers
            WHERE PublicId = @publicId;
            """,
            new { publicId },
            cancellationToken: ct);
}
```

**Métodos disponibles en `MainDapperDbConnection`:**

| Método | Retorno | Uso |
|--------|---------|-----|
| `QueryAsync<T>` | `Task<IEnumerable<T>>` | Múltiples filas |
| `QuerySingleAsync<T>` | `Task<T?>` | 0 o 1 fila |
| `QueryFirstAsync<T>` | `Task<T?>` | Primera fila o null |
| `ExecuteAsync` | `Task<int>` | INSERT / UPDATE / DELETE |
| `ExecuteScalarAsync<T>` | `Task<T>` | COUNT, EXISTS, escalar |

---

## Flujo de una request (OBLIGATORIO)

```
HTTP Request
    ↓
{Modulo}Controller  →  _ = await Mediator.Send(new {Accion}Request(...), ct)
                                ↓
                    {Accion}Handler.Handle(request, ct)
                        return new {Accion}Success(...) | new {Accion}Failure(...)
                                ↓
                    InteractorPipeline  (registrado automáticamente por AddMediator)
                        await Mediator.Publish(response)
                                ↓
                    {Accion}Presenter.Handle(response, ct)
                        _viewModel.Set(success) | _viewModel.OK(data) | _viewModel.Fail(msg)
                                ↓
Controller  →  _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel)
                                ↓
HTTP Response  (siempre ResultViewModel<TController> JSON)
```

- El controller descarta el retorno de `Send` (`_ = await ...`) — la respuesta llega al presenter vía Publish.
- **TODA** respuesta HTTP pasa por `ResultViewModel<TController>` — nunca retornar datos directos.
- El mediador es `Common.Messaging.IMediator` — **nunca MediatR NuGet**.

---

## Patrón de caso de uso

### Request

```csharp
public sealed record GetExampleUserRequest(Guid PublicId) : IRequest<GetExampleUserResponse>;
```

### Responses

```csharp
// Base — abstract, implementa IResponse de Common.Messaging
public abstract record GetExampleUserResponse : IResponse;

// Éxito con DTO único
public sealed record GetExampleUserSuccess(ExampleUserDto Data)
    : GetExampleUserResponse, ISuccess<ExampleUserDto>;

// Éxito con datos custom (paginación, colecciones)
// NUNCA implementar ISuccess<TSelf> con Data => this — referencia circular en JSON
public sealed record GetExampleUsersSuccess(
    IReadOnlyCollection<ExampleUserDto> Users, int Total, int Page, int PageSize)
    : GetExampleUsersResponse, ISuccess;

// Fallo
public sealed record GetExampleUserNotFoundFailure(string Message)
    : GetExampleUserResponse, INotFoundFailure;
```

**Interfaces de resultado (`Common.Results`):**

| Interface | HTTP |
|-----------|------|
| `ISuccess` | 200 |
| `ISuccess<T>` | 200 con propiedad `T Data` |
| `IFailure` | 500 |
| `INotFoundFailure` | 404 |
| `IConflictFailure` | 409 |
| `IValidationFailure` | 400 |

### Handler

```csharp
public sealed class GetExampleUserHandler
    : IRequestHandler<GetExampleUserRequest, GetExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;
    public GetExampleUserHandler(IExampleUserRepository repo) => _repo = repo;

    public async Task<GetExampleUserResponse> Handle(
        GetExampleUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _repo.GetByPublicIdAsync(request.PublicId, cancellationToken);
        if (user is null)
            return new GetExampleUserNotFoundFailure("Usuario no encontrado.");
        return new GetExampleUserSuccess(new ExampleUserDto(...));
    }
}
```

### Presenter

```csharp
// Variante A — success implementa ISuccess<TDto> → _viewModel.Set(success)
public sealed class GetExampleUserPresenter : IPresenter<GetExampleUserResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;
    public GetExampleUserPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(GetExampleUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<ExampleUserDto> success)
            _viewModel.Set(success);
        return Task.CompletedTask;
    }
}

// Variante B — success implementa ISuccess (sin genérico) → _viewModel.OK(success)
// else if (notification is GetExampleUsersSuccess success)
//     _viewModel.OK(success);
```

**Métodos de `ResultViewModel<T>`:**

| Método | Cuándo |
|--------|--------|
| `_viewModel.Set(ISuccess<TDto> s)` | Éxito con `ISuccess<TDto>` — Data = s.Data |
| `_viewModel.OK(object data)` | Éxito con datos custom — Data = el objeto |
| `_viewModel.Fail(string msg)` | Cualquier fallo — IsSuccess = false |

### Controller

```csharp
[Route("api/example/users")]
[Authorize]
public sealed class ExampleUsersController : BaseApiController
{
    private readonly ILogger<ExampleUsersController> _logger;
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public ExampleUsersController(
        IMediator mediator,
        ILogger<ExampleUsersController> logger,
        ResultViewModel<ExampleUsersController> viewModel) : base(mediator)
    { _logger = logger; _viewModel = viewModel; }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new GetExampleUserRequest(id), ct);
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById");
            var innerEx = ex;
            while (innerEx.InnerException != null) innerEx = innerEx.InnerException!;
            return StatusCode(500, _viewModel.Fail(innerEx.Message));
        }
    }
}
```

### Registro DI — `WebApi/ServiceCollectionEx.cs`

```csharp
services.AddScoped(typeof(ResultViewModel<>));
services.AddScoped<INotificationHandler<GetExampleUserResponse>, GetExampleUserPresenter>();
// Un registro por abstract response
```

---

## Convenciones de nomenclatura

| Tipo | Patrón | Ejemplo |
|------|--------|---------|
| Request | `{Accion}Request` | `GetExampleUserRequest` |
| Handler | `{Accion}Handler` | `GetExampleUserHandler` |
| Response base | `{Accion}Response` | `GetExampleUserResponse` |
| Éxito | `{Accion}Success` | `GetExampleUserSuccess` |
| Fallo | `{Accion}{Tipo}Failure` | `GetExampleUserNotFoundFailure` |
| Presenter | `{Accion}Presenter` | `GetExampleUserPresenter` |
| Request body | `{Accion}Body` | `InsertExampleUserBody` |
| Controller | `{Modulo}Controller` | `ExampleUsersController` |
| SQL object | `{Entidad}Sql` | `ExampleUsersSql` |
| Clase marcadora BD | `{Nombre}DbConnection` | `MainDbConnection` |
| Repositorio interfaz | `I{Entidad}Repository` | `IExampleUserRepository` |
| DTO | `{Entidad}Dto` | `ExampleUserDto` |

---

## Migraciones de esquema (PostgreSQL)

Viven en `Host/Services/Schema Migration/Tables/`. Se ejecutan automáticamente al iniciar.

**Numeración:** cada entidad ocupa un bloque de 10 números.

```
X0_<tabla>.sql            — CREATE TABLE IF NOT EXISTS
X1_<tabla>_indexes.sql    — índices
```

**Reglas absolutas:**
- Todos los archivos son idempotentes: `CREATE TABLE IF NOT EXISTS`.
- Nunca editar migraciones ya aplicadas → nueva migración con número mayor.
- Esquema `dbo` para todas las tablas.
- Fechas UTC: `TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))`.

---

## Registro de dependencias (DI)

| Archivo | Responsabilidad |
|---------|----------------|
| `Application/ServiceCollectionEx.cs` | `services.AddMediator(Assembly.GetExecutingAssembly())` |
| `Infrastructure/ServiceCollectionEx.cs` | Factories, `...Sql`, repositorios, servicios externos |
| `WebApi/ServiceCollectionEx.cs` | `ResultViewModel<>`, presenters, controladores |
| `Host/Program.cs` | Composición final, middleware, JWT auth, health, Swagger |

Lifetimes: `MainDbConnectionFactory` → Singleton. `MainDapperDbConnection`, `...Sql`, repositorios, presenters, `ResultViewModel<>` → Scoped.

---

## Autenticación

- JWT HS256: `Jwt:Key` (≥ 32 chars), `Jwt:Issuer`, `Jwt:Audience` desde config / variables de entorno.
- Registrado en `Host/Extensions/JwtAuthExtensions.cs`.
- Nunca poner secretos JWT en `appsettings*.json` — usar variables de entorno en producción.

---

## Observabilidad

- **Logging:** `ILogger<T>` → Serilog → Seq (`http://localhost:5341` en dev). Nunca `Console.WriteLine`.
- **Tracing:** OpenTelemetry OTLP → Jaeger (`http://localhost:16686` en dev).
- **Métricas:** Prometheus en `/metrics`.
- **Health:** `/api/health` — incluye check de PostgreSQL.

---

## Reglas que no se negocian

1. `Application` nunca referencia `Infrastructure`.
2. `WebApi` nunca accede a PostgreSQL ni a repositorios concretos.
3. El mediador es `Common.Messaging.IMediator` — **nunca MediatR NuGet**.
4. Todo SQL vive en clases `...Sql` — cero SQL inline en repositorios, handlers o servicios.
5. Toda respuesta HTTP pasa por `ResultViewModel<TController>` — nunca retornar datos directos.
6. No secretos en `appsettings*.json` — variables de entorno.
7. No editar el submódulo `Common` desde este repositorio.
8. Al terminar cualquier cambio: `dotnet build` desde `Host` con **0 errores**.

---

## Flujo para cambios funcionales

**Antes:** trazar el flujo completo. Identificar la capa correcta de cada pieza.

**Durante:**
1. Nueva tabla → `Host/Services/Schema Migration/Tables/NNN_tabla.sql` + `NNN+1_tabla_indexes.sql`.
2. Nueva entidad → `Domain/Entities/{Modulo}/` + interfaz en `Domain/Repositories/{Modulo}/`.
3. Nueva `...Sql` → `Infrastructure/Persistence/SQLDB/Main/{Modulo}/`.
4. Nuevo repositorio → `Infrastructure/Repositories/{Modulo}/` + DI en `Infrastructure/ServiceCollectionEx.cs`.
5. Nuevo caso de uso → `Application/UseCases/{Modulo}/{Accion}/` (Request + Handler + Responses/).
6. Nuevo presenter → `WebApi/EndPoints/{Modulo}/Presenters/` + registro en `WebApi/ServiceCollectionEx.cs`.

**Al terminar:** `dotnet build` desde `Host` — 0 errores.
