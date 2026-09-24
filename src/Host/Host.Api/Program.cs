using Authentication.Application;
using Authentication.Infrastructure;
using Authentication.Presentation;
using Common.Logging;
using Common.Messaging;
using Common.MultiTenancy;
using Common.Observability;
using Common.Web;
using Host.Api.Extensions;
using Shared.Infrastructure;
using Shared.Kernel.Context;
using Shared.Web;
using Shared.Web.Tenancy;
using Tenancy.Application;
using Tenancy.Infrastructure;
using Tenancy.Presentation;
using Users.Application;
using Users.Infrastructure;
using Users.Presentation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLoggingServices(builder.Configuration);
builder.Services.AddObservability(
    builder.Configuration,
    builder.Configuration["Observability:MeterName"] ?? "BackTemplate.Api");

// Multi-tenancy
builder.Services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

// Usuario de la peticion (auditoria de SaveChanges y bitacora).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// Base de datos: un solo DbContext; cada modulo aporta sus tablas. Las migraciones NO corren al arrancar:
// son un paso aparte, con el rol dueno del esquema (ver docs/adr y scripts/dev-db.sh).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Falta ConnectionStrings:DefaultConnection. Copia .env.example a .env o define ConnectionStrings__DefaultConnection.");
builder.Services.AddAppDatabase(connectionString);

// Mediador de Common, SIN escaneo de ensamblados: cada modulo registra sus handlers.
builder.Services.AddMediator();

builder.Services.AddTenancyApplicationServices();
builder.Services.AddTenancyInfrastructureServices();
builder.Services.AddTenancyWebApiServices();

builder.Services.AddUsersApplicationServices();
builder.Services.AddUsersInfrastructureServices();
builder.Services.AddUsersWebApiServices();

builder.Services.AddAuthenticationApplicationServices();
builder.Services.AddAuthenticationInfrastructureServices(builder.Configuration);
builder.Services.AddAuthenticationWebApiServices();

// Un solo formato de error para toda la API (filtro global, modelo invalido, middleware, estado vacio).
builder.Services.AddApiControllers();

builder.Services.AddHealthServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddLocalhostCors();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

app.UseApiErrorHandling();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local") || app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCorrelationId();

app.UseCors(CorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>(); // DESPUES de autenticar: el tenant sale del JWT ya validado.

app.MapControllers();
app.MapHealth();

app.Run();

/// <summary>Expuesto para <c>WebApplicationFactory&lt;Program&gt;</c> en las pruebas de integracion.</summary>
public partial class Program;
