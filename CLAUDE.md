# CLAUDE.md

## Propósito

Guía de referencia para Claude Code al trabajar en este repositorio (`back-template`).

Este documento describe la arquitectura, convenciones y reglas que deben respetarse en todo cambio, generación o revisión de código.

---

## Arquitectura

**Patrón:** Clean Architecture + CQRS + Mediator + Presenter — **Monolito Modular**

**Módulos actuales:** `Auth` (Login + RefreshToken) · `Users` (CRUD)

**Multi-tenancy:** Tenant = empresa / Branch = sucursal. Tenant y Branch llegan al backend vía claims JWT (`tenant_id`, `branch_id` como BIGINT string). El controller los extrae de `User.FindFirstValue(...)` y los pasa al Request. `TenantClaimsMiddleware` los inyecta en `ITenantContextAccessor` para Serilog/OTel.

**Capas y dirección de dependencias:**

```
Domain
Application    → Domain
Infrastructure → Application + Domain + Common
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
│   ├── Entities/
│   │   ├── Auth/RefreshToken.cs
│   │   ├── Branches/Branch.cs
│   │   ├── Tenants/Tenant.cs
│   │   └── Users/User.cs
│   └── Repositories/
│       ├── Auth/IRefreshTokenRepository.cs
│       └── Users/IUserRepository.cs
├── Application/
│   ├── Dto/
│   │   ├── Auth/TokenDto.cs
│   │   └── Users/UserDto.cs
│   ├── Services/
│   │   ├── IJwtTokenService.cs
│   │   └── IPasswordHasher.cs
│   ├── UseCases/
│   │   ├── Auth/
│   │   │   ├── Login/            ← LoginRequest, LoginHandler, Responses/
│   │   │   └── RefreshToken/     ← RefreshTokenRequest, RefreshTokenHandler, Responses/
│   │   └── Users/
│   │       ├── GetUser/          ← GetUserRequest, GetUserHandler, Responses/
│   │       ├── GetUsers/         ← GetUsersRequest, GetUsersHandler, Responses/
│   │       ├── CreateUser/       ← CreateUserRequest, CreateUserHandler, Responses/
│   │       ├── UpdateUser/       ← UpdateUserRequest, UpdateUserHandler, Responses/
│   │       └── DisableUser/      ← DisableUserRequest, DisableUserHandler, Responses/
│   └── ServiceCollectionEx.cs
├── Infrastructure/
│   ├── PostgreSql/
│   │   ├── MainDbConnection.cs              ← clase marcadora (clave ConnectionStrings)
│   │   ├── MainDbConnectionFactory.cs       ← abre NpgsqlConnections
│   │   └── MainDapperDbConnection.cs        ← ÚNICO punto de ejecución SQL
│   ├── Persistence/SQLDB/Main/
│   │   ├── Auth/RefreshTokensSql.cs
│   │   └── Users/UsersSql.cs
│   ├── Repositories/
│   │   ├── Auth/RefreshTokenRepository.cs
│   │   └── Users/UserRepository.cs
│   ├── Services/
│   │   ├── JwtTokenService.cs               ← implementa IJwtTokenService
│   │   └── PasswordHasher.cs                ← implementa IPasswordHasher (BCrypt)
│   └── ServiceCollectionEx.cs
├── WebApi/
│   ├── Base/BaseApiController.cs
│   ├── EndPoints/
│   │   ├── Auth/
│   │   │   ├── AuthController.cs
│   │   │   ├── Presenters/LoginPresenter.cs + RefreshTokenPresenter.cs
│   │   │   └── RequestBodies/LoginBody.cs + RefreshTokenBody.cs
│   │   └── Users/
│   │       ├── UsersController.cs
│   │       ├── Presenters/GetUserPresenter.cs + GetUsersPresenter.cs + CreateUserPresenter.cs + UpdateUserPresenter.cs + DisableUserPresenter.cs
│   │       └── RequestBodies/CreateUserBody.cs + UpdateUserBody.cs
│   └── ServiceCollectionEx.cs
├── Host/
│   ├── Program.cs
│   ├── Extensions/
│   │   ├── JwtAuthExtensions.cs             ← AddJwtAuthentication(config)
│   │   ├── CorsExtensions.cs                ← AddLocalhostCors() + PolicyName
│   │   ├── SwaggerExtensions.cs             ← AddSwaggerWithJwt()
│   │   └── HealthExtensions.cs              ← AddHealthServices(config) + MapHealth()
│   ├── Middleware/TenantClaimsMiddleware.cs  ← extrae tenant_id del JWT → ITenantContextAccessor
│   ├── appsettings.json
│   ├── appsettings.Local.json               ← dev diario, SQL text logging activo
│   ├── appsettings.Development.json
│   ├── appsettings.Staging.json
│   ├── appsettings.Production.json
│   └── Services/Schema Migration/Tables/*.sql
├── Tests/
└── Common/   (submódulo — NO editar desde este repo)
```

---

## Stack tecnológico

| Categoría | Tecnología |
|-----------|-----------|
| Runtime | .NET 10 / C# 13 |
| Framework | ASP.NET Core 10 |
| Base de datos | PostgreSQL 17 |
| ORM | Dapper (raw SQL parametrizado) |
| Driver | Npgsql 10 |
| Mediator | Custom — `Common.Messaging` (NO MediatR NuGet) |
| Auth | JWT Bearer HS256 |
| Passwords | BCrypt.Net-Next (workFactor: 12) |
| Logging | Serilog → Seq |
| Tracing | OpenTelemetry OTLP → Jaeger |
| Métricas | Prometheus en `/metrics` |
| Health | `/api/health` |
| Testing | xUnit |
| Deploy | Docker multi-stage |

---

## Esquema de BD (módulos actuales)

| Tabla | Descripción |
|-------|-------------|
| `dbo.Tenants` | 001-002 — Empresas SaaS |
| `dbo.Branches` | 010-011 — Sucursales de un tenant |
| `dbo.Users` | 020-021 — Usuarios con TenantId + BranchId FK |
| `dbo.RefreshTokens` | 030-031 — Tokens de actualización JWT |

**Numeración de migraciones:** bloques de 10 por entidad. Próxima entidad comienza en `040`.

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
public sealed class UsersSql
{
    private readonly MainDapperDbConnection _db;
    public UsersSql(MainDapperDbConnection db) => _db = db;

    public Task<User?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
        _db.QuerySingleAsync<User>(
            """
            SELECT Id, PublicId, TenantId, BranchId, FullName, Email, Role, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.Users
            WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { publicId, tenantId },
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
{Modulo}Controller  →  var result = await Mediator.Send(new {Accion}Request(...), ct)
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
Controller  →  if (_viewModel.IsSuccess) return Ok(_viewModel);
               return result is XxxNotFoundFailure ? NotFound(_viewModel) : StatusCode(500, _viewModel);
                                ↓
HTTP Response  (siempre ResultViewModel<TController> JSON)
```

- El controller captura el `result` de `Send()` **solo** para determinar el HTTP status code.
- Los datos de la respuesta siempre vienen del ViewModel (set por el Presenter).
- **TODA** respuesta HTTP pasa por `ResultViewModel<TController>` — nunca retornar datos directos.
- El mediador es `Common.Messaging.IMediator` — **nunca MediatR NuGet**.

---

## Patrón de caso de uso

### Request (con contexto tenant)

```csharp
public sealed record GetUserRequest(Guid PublicId, long TenantId) : IRequest<GetUserResponse>;
```

El controller extrae `TenantId` y `BranchId` directamente de los claims JWT:

```csharp
private long CurrentTenantId =>
    long.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : 0;
private long CurrentBranchId =>
    long.TryParse(User.FindFirstValue("branch_id"), out var id) ? id : 0;
```

### Responses

```csharp
// Base — abstract, implementa IResponse de Common.Messaging
public abstract record GetUserResponse : IResponse;

// Éxito con DTO único
public sealed record GetUserSuccess(UserDto Data) : GetUserResponse, ISuccess<UserDto>;

// Éxito con datos custom (paginación, colecciones)
// NUNCA implementar ISuccess<TSelf> con Data => this — referencia circular en JSON
public sealed record GetUsersSuccess(
    IReadOnlyCollection<UserDto> Users, int Total, int Page, int PageSize)
    : GetUsersResponse, ISuccess;

// Fallo
public sealed record GetUserNotFoundFailure(string Message) : GetUserResponse, INotFoundFailure;
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
public sealed class GetUserHandler : IRequestHandler<GetUserRequest, GetUserResponse>
{
    private readonly IUserRepository _users;
    public GetUserHandler(IUserRepository users) => _users = users;

    public async Task<GetUserResponse> Handle(GetUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByPublicIdAsync(request.PublicId, request.TenantId, cancellationToken);
        if (user is null)
            return new GetUserNotFoundFailure("Usuario no encontrado.");
        return new GetUserSuccess(new UserDto(...));
    }
}
```

### Presenter

```csharp
// Variante A — success implementa ISuccess<TDto> → _viewModel.Set(success)
public sealed class GetUserPresenter : INotificationHandler<GetUserResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;
    public GetUserPresenter(ResultViewModel<UsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(GetUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<UserDto> success)
            _viewModel.Set(success);
        return Task.CompletedTask;
    }
}

// Variante B — success implementa ISuccess (sin genérico) → _viewModel.OK(success)
// else if (notification is GetUsersSuccess success)
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
[Route("api/users")]
[Authorize]
public sealed class UsersController : BaseApiController
{
    private readonly ILogger<UsersController>        _logger;
    private readonly ResultViewModel<UsersController> _viewModel;

    public UsersController(
        IMediator mediator,
        ILogger<UsersController> logger,
        ResultViewModel<UsersController> viewModel) : base(mediator)
    { _logger = logger; _viewModel = viewModel; }

    private long CurrentTenantId =>
        long.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : 0;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(new GetUserRequest(id, CurrentTenantId), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is GetUserNotFoundFailure
                ? NotFound(_viewModel)
                : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById User");
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }
}
```

### Registro DI — `WebApi/ServiceCollectionEx.cs`

```csharp
services.AddScoped(typeof(ResultViewModel<>));
services.AddScoped<INotificationHandler<GetUserResponse>, GetUserPresenter>();
// Un registro por abstract response
```

---

## Servicios de dominio (Application.Services)

| Interfaz | Implementación | Descripción |
|----------|---------------|-------------|
| `IJwtTokenService` | `Infrastructure/Services/JwtTokenService.cs` | Genera access token (HS256) + refresh token |
| `IPasswordHasher` | `Infrastructure/Services/PasswordHasher.cs` | Hash/Verify con BCrypt (workFactor: 12) |

**Claims del JWT:**

| Claim | Valor |
|-------|-------|
| `sub` | user.PublicId (UUID) |
| `email` | user.Email |
| `role` | user.Role ("Admin" / "User") |
| `tenant_id` | user.TenantId (long como string) |
| `branch_id` | user.BranchId (long como string) |

**Config necesaria en appsettings:**
```json
"Jwt": {
  "Key": "...",
  "Issuer": "...",
  "Audience": "...",
  "ExpirationMinutes": 60,
  "RefreshTokenExpiryDays": 30
}
```

---

## Convenciones de nomenclatura

| Tipo | Patrón | Ejemplo |
|------|--------|---------|
| Request | `{Accion}Request` | `GetUserRequest` |
| Handler | `{Accion}Handler` | `GetUserHandler` |
| Response base | `{Accion}Response` | `GetUserResponse` |
| Éxito | `{Accion}Success` | `GetUserSuccess` |
| Fallo | `{Accion}{Tipo}Failure` | `GetUserNotFoundFailure` |
| Presenter | `{Accion}Presenter` | `GetUserPresenter` |
| Request body | `{Accion}Body` | `CreateUserBody` |
| Controller | `{Modulo}Controller` | `UsersController` |
| SQL object | `{Entidad}Sql` | `UsersSql` |
| Clase marcadora BD | `{Nombre}DbConnection` | `MainDbConnection` |
| Repositorio interfaz | `I{Entidad}Repository` | `IUserRepository` |
| DTO | `{Entidad}Dto` | `UserDto` |

---

## Migraciones de esquema (PostgreSQL)

Viven en `Host/Services/Schema Migration/Tables/`. Se ejecutan automáticamente al iniciar.

**Numeración:** 3 dígitos, bloques de 10 por entidad.

```
NNN_<tabla>.sql            — CREATE TABLE IF NOT EXISTS
NNN+1_<tabla>_indexes.sql  — índices
```

Bloques actuales: Tenants=001, Branches=010, Users=020, RefreshTokens=030. **Próxima entidad: 040.**

**Reglas absolutas:**
- Todos los archivos son idempotentes: `CREATE TABLE IF NOT EXISTS`.
- Nunca editar migraciones ya aplicadas → nueva migración con número mayor.
- Esquema `dbo` para todas las tablas.
- Fechas UTC: `TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))`.

---

## Registro de dependencias (DI)

| Archivo | Responsabilidad |
|---------|----------------|
| `Application/ServiceCollectionEx.cs` | `AddApplicationServices()` — mediator + handlers |
| `Infrastructure/ServiceCollectionEx.cs` | `AddInfrastructureServices(config)` — factories, SQL objects, repositorios, servicios de dominio, ITenantContextAccessor |
| `WebApi/ServiceCollectionEx.cs` | `AddWebApiServices()` — `ResultViewModel<>`, presenters, controllers |
| `Host/Extensions/JwtAuthExtensions.cs` | `AddJwtAuthentication(config)` — JWT HS256 |
| `Host/Extensions/CorsExtensions.cs` | `AddLocalhostCors()` — CORS localhost:\* |
| `Host/Extensions/SwaggerExtensions.cs` | `AddSwaggerWithJwt()` — Swagger + Bearer UI |
| `Host/Extensions/HealthExtensions.cs` | `AddHealthServices(config)` + `MapHealth()` — `/api/health` |
| `Host/Program.cs` | Composición final + `UseMiddleware<TenantClaimsMiddleware>()` |

Lifetimes: `MainDbConnectionFactory`, `ITenantContextAccessor` → Singleton. `MainDapperDbConnection`, `...Sql`, repositorios, servicios, presenters, `ResultViewModel<>` → Scoped.

---

## Autenticación

- JWT HS256: `Jwt:Key` (≥ 32 chars), `Jwt:Issuer`, `Jwt:Audience` desde config / variables de entorno.
- Registrado en `Host/Extensions/JwtAuthExtensions.cs`.
- Login: `POST /api/auth/login` → `{ accessToken, refreshToken, expiresAtUtc }`.
- Refresh: `POST /api/auth/refresh` → nuevo par de tokens.
- Roles: `[Authorize(Roles = "Admin")]` para endpoints de escritura en Users.
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
7. Nuevo controller (o nuevo endpoint) → `WebApi/EndPoints/{Modulo}/{Modulo}Controller.cs` extiende `BaseApiController`, inyecta `ResultViewModel<{Modulo}Controller>` y `ILogger<>`.

**Al terminar:** `dotnet build` desde `Host` — 0 errores.
