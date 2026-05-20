# Program.cs — El archivo que ata todo

`Host.Api/Program.cs` es el punto de composición del sistema. Es el único lugar del proyecto donde todos los módulos se conocen entre sí. El resto del sistema nunca cruza las fronteras de módulo.

---

## El archivo completo

```csharp
using Authentication.Application;
using Authentication.Infrastructure;
using Authentication.Presentation;
using Common.Logging;
using Common.Messaging;
using Common.MultiTenancy;
using Common.Observability;
using Common.Web;
using Host.Api.Extensions;
using Host.Api.Middleware;
using Shared.Database;
using Tenancy.Application;
using Tenancy.Infrastructure;
using Tenancy.Presentation;
using Users.Application;
using Users.Infrastructure;
using Users.Presentation;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────
// 1. OBSERVABILIDAD — primero para que los logs
//    capturen cualquier error durante el startup
// ─────────────────────────────────────────────
builder.Services.AddLoggingServices(builder.Configuration);
builder.Services.AddObservability(builder.Configuration);

// ─────────────────────────────────────────────
// 2. INFRAESTRUCTURA COMPARTIDA
// ─────────────────────────────────────────────
builder.Services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

// AppDbContext + DatabaseInitializationService (EnsureCreated al startup)
builder.Services.AddMainDatabase(builder.Configuration);

// ─────────────────────────────────────────────
// 3. MEDIATOR — UNA SOLA LLAMADA con todos los
//    ensamblados Application de todos los módulos
// ─────────────────────────────────────────────
builder.Services.AddMediator(
    typeof(Tenancy.Application.ServiceCollectionEx).Assembly,
    typeof(Users.Application.ServiceCollectionEx).Assembly,
    typeof(Authentication.Application.ServiceCollectionEx).Assembly
);

// ─────────────────────────────────────────────
// 4. MÓDULOS — Application + Infrastructure + Presentation
//    por cada módulo, en cualquier orden
// ─────────────────────────────────────────────
builder.Services.AddTenancyApplicationServices();
builder.Services.AddTenancyInfrastructureServices();
builder.Services.AddTenancyWebApiServices();

builder.Services.AddUsersApplicationServices();
builder.Services.AddUsersInfrastructureServices();
builder.Services.AddUsersWebApiServices();

builder.Services.AddAuthenticationApplicationServices();
builder.Services.AddAuthenticationInfrastructureServices(builder.Configuration);
builder.Services.AddAuthenticationWebApiServices();

// ─────────────────────────────────────────────
// 5. INFRAESTRUCTURA DEL HOST
// ─────────────────────────────────────────────
builder.Services.AddHealthServices(builder.Configuration);

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddLocalhostCors();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

// ─────────────────────────────────────────────
// BUILD — a partir de aquí no se puede agregar
//         más servicios al DI container
// ─────────────────────────────────────────────
var app = builder.Build();

// ─────────────────────────────────────────────
// 6. SWAGGER — solo en entornos no-Production
// ─────────────────────────────────────────────
if (app.Environment.IsDevelopment()       ||
    app.Environment.IsEnvironment("Local") ||
    app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ─────────────────────────────────────────────
// 7. PIPELINE DE MIDDLEWARE — el orden importa
// ─────────────────────────────────────────────
app.UseCoreProblemDetails();          // manejo global de excepciones — siempre primero
app.UseCorrelationId();               // agrega X-Correlation-Id al request y response

app.UseCors(CorsExtensions.PolicyName);
app.UseAuthentication();              // verifica el JWT
app.UseAuthorization();               // verifica roles y policies
app.UseMiddleware<TenantClaimsMiddleware>(); // extrae tenant_id del JWT validado

// ─────────────────────────────────────────────
// 8. ENDPOINTS
// ─────────────────────────────────────────────
app.MapControllers();
app.MapHealth();
app.MapPrometheusScrapingEndpoint();

app.Run();
```

---

## Por qué ese orden — sección por sección

### 1. Observabilidad primero

`AddLoggingServices` y `AddObservability` configuran Serilog y OpenTelemetry. Se registran primero para que cualquier error que ocurra durante la configuración de los demás servicios ya quede logueado correctamente.

### 2. Infraestructura compartida

`ITenantContextAccessor` debe registrarse antes que `AddMainDatabase` porque el `AppDbContext` lo inyecta vía constructor (en realidad el factory captura la lambda, pero el scope se crea antes del request).

`AddMainDatabase(builder.Configuration)` registra `AppDbContext` como Scoped (via `AddDbContext`) y registra `DatabaseInitializationService` como `IHostedService`. Al iniciar la app, este servicio crea un scope, establece un tenant dummy `"0"`, y llama `EnsureCreatedAsync()` para crear las tablas.

### 3. Mediator — por qué una sola llamada

`AddMediator(assembly1, assembly2, ...)` hace tres cosas:
1. Registra `IMediator` → `Mediator` (Scoped)
2. Registra `IPipelineBehavior<,>` → `InteractorPipeline<,>` (Scoped)
3. Escanea cada assembly en busca de `IRequestHandler<,>` y los registra (Scoped)

Si se llama `AddMediator` N veces, el pipeline queda registrado N veces. Al publicar una response, el mediator la ejecuta a través de todos los pipelines encadenados — cada handler se ejecuta N veces. **Una sola llamada con todos los assemblies.**

Los Presenters (`INotificationHandler<TResponse>`) **no** se pasan a `AddMediator`. Si estuvieran en la lista, el scan los registraría automáticamente además de los registros manuales de `ServiceCollectionEx`, causando doble invocación. Los presenters se registran solo en `{Modulo}.Presentation/ServiceCollectionEx.cs`.

### 4. Módulos — los tres métodos por módulo

Cada módulo expone tres extension methods:

| Método | Qué registra |
|--------|-------------|
| `Add{Modulo}ApplicationServices()` | Servicios propios de Application (generalmente vacío) |
| `Add{Modulo}InfrastructureServices()` | Repositorios concretos (que inyectan `AppDbContext`) |
| `Add{Modulo}WebApiServices()` | `ResultViewModel<>` + presenters + `AddApplicationPart` |

`AddApplicationPart` en la línea de `AddUsersWebApiServices()` es lo que hace que ASP.NET Core descubra los controllers de ese módulo. Sin esa línea, `UsersController` no sería encontrado porque no está en el ensamblado de `Host.Api`.

### 5. Infraestructura del host

`AddHealthServices` configura el health check de PostgreSQL. Los demás registros son JWT, CORS, y Swagger.

### 6. Swagger condicional

Swagger solo se activa en entornos no-Production. La condición incluye `Local`, `Development` y `Staging`. En `Production` no se expone la documentación de la API.

### 7. Pipeline de middleware — el orden es crítico

```
Request entrante
    ↓
UseCoreProblemDetails   ← envuelve todo lo que sigue; captura excepciones
    ↓
UseCorrelationId        ← agrega header X-Correlation-Id
    ↓
UseCors                 ← headers CORS (antes de Auth para manejar preflight)
    ↓
UseAuthentication       ← verifica y decodifica el JWT → llena HttpContext.User
    ↓
UseAuthorization        ← verifica [Authorize] y roles → 401/403 si falla
    ↓
TenantClaimsMiddleware  ← lee tenant_id de HttpContext.User (ya autenticado)
                          → alimenta ITenantContextAccessor
                          → el AppDbContext del request ya puede usarlo
    ↓
MapControllers          ← enruta al controller correcto
```

**Por qué `TenantClaimsMiddleware` va después de `UseAuthorization`:** el middleware lee claims del JWT. Si va antes de `UseAuthentication`, `HttpContext.User` todavía no tiene los claims — `User.FindFirst("tenant_id")` devuelve null. Además, necesita ir después de `UseAuthorization` para que endpoints anónimos (login) no fallen por un tenant vacío.

**Por qué `UseCoreProblemDetails` va primero:** actúa como un try-catch global que envuelve todo el pipeline. Si cualquier middleware o controller lanza una excepción no capturada, la convierte en una respuesta RFC 7807 con el status code correcto.

---

## Agregar un nuevo módulo — cambios en Program.cs

Solo se necesitan dos cosas:

**1. Pasar el ensamblado Application al AddMediator existente:**
```csharp
builder.Services.AddMediator(
    typeof(Tenancy.Application.ServiceCollectionEx).Assembly,
    typeof(Users.Application.ServiceCollectionEx).Assembly,
    typeof(Authentication.Application.ServiceCollectionEx).Assembly,
    typeof(Inventory.Application.ServiceCollectionEx).Assembly   // ← agregar
);
```

**2. Llamar los tres métodos del módulo:**
```csharp
builder.Services.AddInventoryApplicationServices();
builder.Services.AddInventoryInfrastructureServices();
builder.Services.AddInventoryWebApiServices();
```

Eso es todo. No hay nada más que tocar en Program.cs.

---

## Qué NO pertenece en Program.cs

- Lógica de negocio — va en handlers
- Configuración de cada módulo — va en su `ServiceCollectionEx`
- Acceso a datos — va en repositorios vía `AppDbContext`
- Validaciones de request — van en handlers o middleware específico

Program.cs es solo composición: conecta las piezas, no implementa nada.
