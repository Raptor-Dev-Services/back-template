using Common.Messaging;
using Common.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Kernel.Audit;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Results;
using Shared.Web;
using Tenancy.Application.UseCases.AutomatedTasks.GetAutomatedTaskHistory.Responses;
using Tenancy.Application.UseCases.AutomatedTasks.ListAutomatedTasks.Responses;
using Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow.Responses;
using Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled.Responses;
using Tenancy.Application.UseCases.Files;
using Tenancy.Application.UseCases.Files.GetFileUrl.Responses;
using Tenancy.Application.UseCases.Files.GetFileUrls.Responses;
using Tenancy.Application.UseCases.Files.UploadFile.Responses;
using Tenancy.Application.UseCases.GetAuditLog.Responses;
using Tenancy.Presentation.Controllers;

namespace Tenancy.Presentation;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddTenancyWebApiServices(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ResultViewModel<>));
        services.AddScoped<INotificationHandler<GetAuditLogResponse>, GetAuditLogPresenter>();
        services.AddScoped<INotificationHandler<ListAutomatedTasksResponse>, ListAutomatedTasksPresenter>();
        services.AddScoped<INotificationHandler<SetAutomatedTaskEnabledResponse>, SetAutomatedTaskEnabledPresenter>();
        services.AddScoped<INotificationHandler<RunAutomatedTaskNowResponse>, RunAutomatedTaskNowPresenter>();
        services.AddScoped<INotificationHandler<GetAutomatedTaskHistoryResponse>, GetAutomatedTaskHistoryPresenter>();
        services.AddScoped<INotificationHandler<UploadFileResponse>, UploadFilePresenter>();
        services.AddScoped<INotificationHandler<GetFileUrlResponse>, GetFileUrlPresenter>();
        services.AddScoped<INotificationHandler<GetFileUrlsResponse>, GetFileUrlsPresenter>();

        services.AddControllers().AddApplicationPart(typeof(ServiceCollectionEx).Assembly);
        return services;
    }
}

internal sealed class GetAuditLogPresenter(ResultViewModel<AuditLogController> vm)
    : ResultPresenter<AuditLogController, GetAuditLogResponse, PagedResult<AuditEntryDto>>(vm);

internal sealed class ListAutomatedTasksPresenter(ResultViewModel<AutomatedTasksController> vm)
    : ResultPresenter<AutomatedTasksController, ListAutomatedTasksResponse, IReadOnlyList<AutomatedTaskStatusDto>>(vm);

internal sealed class SetAutomatedTaskEnabledPresenter(ResultViewModel<AutomatedTasksController> vm)
    : ResultPresenter<AutomatedTasksController, SetAutomatedTaskEnabledResponse, AutomatedTaskStatusDto>(vm);

internal sealed class RunAutomatedTaskNowPresenter(ResultViewModel<AutomatedTasksController> vm)
    : ResultPresenter<AutomatedTasksController, RunAutomatedTaskNowResponse, AutomatedTaskRunDto>(vm);

internal sealed class GetAutomatedTaskHistoryPresenter(ResultViewModel<AutomatedTasksController> vm)
    : ResultPresenter<AutomatedTasksController, GetAutomatedTaskHistoryResponse, PagedResult<AutomatedTaskRunDto>>(vm);

internal sealed class UploadFilePresenter(ResultViewModel<FilesController> vm)
    : ResultPresenter<FilesController, UploadFileResponse, UploadedFileDto>(vm);

internal sealed class GetFileUrlPresenter(ResultViewModel<FilesController> vm)
    : ResultPresenter<FilesController, GetFileUrlResponse, FileUrlDto>(vm);

internal sealed class GetFileUrlsPresenter(ResultViewModel<FilesController> vm)
    : ResultPresenter<FilesController, GetFileUrlsResponse, FileUrlsDto>(vm);
