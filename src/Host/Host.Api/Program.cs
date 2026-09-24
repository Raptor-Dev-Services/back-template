using Authentication.Application;
using Authentication.Infrastructure;
using Authentication.Presentation;
using Common.Messaging;
using Common.MultiTenancy;
using Common.Observability;
using Host.Api.Extensions;
using Shared.Infrastructure;
using Shared.Infrastructure.Email;
using Shared.Kernel.Context;
using Shared.Web;
using Shared.Web.Tenancy;
using Tenancy.Application;
using Tenancy.Infrastructure;
using Tenancy.Presentation;
using Users.Application;
using Users.Infrastructure;
using Users.Presentation;

// PRIMERO: el .env de desarrollo tiene que estar en el entorno antes de que el builder lea la configuracion.
AppConfigurationExtensions.LoadDotEnvInDevelopment();

var builder = WebApplication.CreateBuilder(args);

// El contenedor se valida en TODOS los entornos (el default solo lo hace en Development): un registro que falta o
// un Scoped capturado por un Singleton se detecta al arrancar, no con el primer usuario que toca ese endpoint.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Configuration.EnsureProductionSettings(builder.Environment);

// Logs: Serilog con contexto de peticion. Trazas y metricas: OpenTelemetry por OTLP (Common), sin exportador
// Prometheus (era un paquete beta) y con el nombre del meter configurable.
builder.AddAppLogging();
builder.Services.AddObservability(
    builder.Configuration,
    builder.Configuration["Observability:MeterName"] ?? "BackTemplate.Api");

// --- Contexto de la peticion ------------------------------------------------------------------
// El tenant lo publica TenantContextMiddleware desde el JWT; el usuario lo lee HttpCurrentUser.
builder.Services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// --- Base de datos ----------------------------------------------------------------------------
// Un solo DbContext; cada modulo aporta sus tablas. Las migraciones NO corren al arrancar: son un paso aparte,
// con el rol dueno del esquema (scripts/dev-db.sh en desarrollo, el pipeline de deploy en produccion).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Falta ConnectionStrings:DefaultConnection. Copia .env.example a .env o define ConnectionStrings__DefaultConnection.");
builder.Services.AddAppDatabase(connectionString);

// --- Servicios transversales ------------------------------------------------------------------
builder.Services.AddAppEmail(builder.Configuration);

// Mediador de Common, SIN escaneo de ensamblados: cada modulo registra sus handlers.
builder.Services.AddMediator();

// --- Modulos ----------------------------------------------------------------------------------
builder.Services.AddTenancyApplicationServices();
builder.Services.AddTenancyInfrastructureServices();
builder.Services.AddTenancyWebApiServices();

builder.Services.AddUsersApplicationServices();
builder.Services.AddUsersInfrastructureServices();
builder.Services.AddUsersWebApiServices();

builder.Services.AddAuthenticationApplicationServices();
builder.Services.AddAuthenticationInfrastructureServices();
builder.Services.AddAuthenticationWebApiServices();

// --- API --------------------------------------------------------------------------------------
builder.Services.AddApiControllers();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddHttpEdge(builder.Configuration, builder.Environment);
builder.Services.AddHealthServices(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt(builder.Configuration);

var app = builder.Build();

// --- Pipeline (el orden es parte de la seguridad; ver HttpEdgeExtensions.UseHttpEdge) -----------
app.UseHttpEdge();          // IP real, correlacion, cabeceras de seguridad, HSTS
app.UseAppRequestLogging(); // un evento por peticion, sin query string; salud a Verbose
app.UseApiErrorHandling();  // un solo formato de error para todo lo de abajo
app.UseSwaggerIfEnabled();

app.UseCors(HttpEdgeExtensions.CorsPolicy);
app.UseAuthentication();
app.UseRateLimitingIfEnabled();               // despues de autenticar: la politica por usuario lee el sub validado
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>(); // despues de autenticar: el tenant sale del JWT ya validado

app.MapControllers();
app.MapHealth();

app.Run();

/// <summary>Expuesto para <c>WebApplicationFactory&lt;Program&gt;</c> en las pruebas de integracion.</summary>
public partial class Program;
