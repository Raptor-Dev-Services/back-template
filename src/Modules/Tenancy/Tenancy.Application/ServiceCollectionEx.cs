using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Tenancy.Application.Api;
using Tenancy.Application.UseCases.GetAuditLog;
using Tenancy.Application.UseCases.GetAuditLog.Responses;
using Tenancy.Contracts.Interfaces;

namespace Tenancy.Application;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddTenancyApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITenancyApi, TenancyApi>();
        services.AddScoped<IRequestHandler<GetAuditLogRequest, GetAuditLogResponse>, GetAuditLogHandler>();
        return services;
    }
}
