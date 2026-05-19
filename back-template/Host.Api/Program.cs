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

builder.Services.AddLoggingServices(builder.Configuration);
builder.Services.AddObservability(builder.Configuration);

// Multi-tenancy
builder.Services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

// Database — EF Core + schema initialization
builder.Services.AddMainDatabase(builder.Configuration);

// Mediator — single call with all Application assemblies
builder.Services.AddMediator(
    typeof(Tenancy.Application.ServiceCollectionEx).Assembly,
    typeof(Users.Application.ServiceCollectionEx).Assembly,
    typeof(Authentication.Application.ServiceCollectionEx).Assembly
);

// Tenancy module
builder.Services.AddTenancyApplicationServices();
builder.Services.AddTenancyInfrastructureServices();
builder.Services.AddTenancyWebApiServices();

// Users module
builder.Services.AddUsersApplicationServices();
builder.Services.AddUsersInfrastructureServices();
builder.Services.AddUsersWebApiServices();

// Authentication shared
builder.Services.AddAuthenticationApplicationServices();
builder.Services.AddAuthenticationInfrastructureServices(builder.Configuration);
builder.Services.AddAuthenticationWebApiServices();

// Infrastructure
builder.Services.AddHealthServices(builder.Configuration);

// Auth & API
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddLocalhostCors();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

if (app.Environment.IsDevelopment()                  ||
    app.Environment.IsEnvironment("Local")            ||
    app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCoreProblemDetails();
app.UseCorrelationId();

app.UseCors(CorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantClaimsMiddleware>();

app.MapControllers();
app.MapHealth();
app.MapPrometheusScrapingEndpoint();

app.Run();
