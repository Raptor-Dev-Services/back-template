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
using Shared.Infrastructure;
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

// Database
builder.Services.AddMainDatabase(builder.Configuration);

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
builder.Services.AddControllers().AddApiErrorHandling();

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
app.UseMiddleware<TenantClaimsMiddleware>();

app.MapControllers();
app.MapHealth();

app.Run();

/// <summary>Expuesto para <c>WebApplicationFactory&lt;Program&gt;</c> en las pruebas de integracion.</summary>
public partial class Program;
