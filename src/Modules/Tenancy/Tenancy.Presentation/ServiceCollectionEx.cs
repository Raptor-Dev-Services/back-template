using Common.Messaging;
using Common.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Kernel.Audit;
using Shared.Kernel.Results;
using Shared.Web;
using Tenancy.Application.UseCases.GetAuditLog.Responses;
using Tenancy.Presentation.Controllers;

namespace Tenancy.Presentation;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddTenancyWebApiServices(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ResultViewModel<>));
        services.AddScoped<INotificationHandler<GetAuditLogResponse>, GetAuditLogPresenter>();

        services.AddControllers().AddApplicationPart(typeof(ServiceCollectionEx).Assembly);
        return services;
    }
}

internal sealed class GetAuditLogPresenter(ResultViewModel<AuditLogController> vm)
    : ResultPresenter<AuditLogController, GetAuditLogResponse, PagedResult<AuditEntryDto>>(vm);
