using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Tenancy.Application.Api;
using Tenancy.Application.UseCases.AutomatedTasks.GetAutomatedTaskHistory;
using Tenancy.Application.UseCases.AutomatedTasks.GetAutomatedTaskHistory.Responses;
using Tenancy.Application.UseCases.AutomatedTasks.ListAutomatedTasks;
using Tenancy.Application.UseCases.AutomatedTasks.ListAutomatedTasks.Responses;
using Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow;
using Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow.Responses;
using Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled;
using Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled.Responses;
using Tenancy.Application.UseCases.Files.GetFileUrl;
using Tenancy.Application.UseCases.Files.GetFileUrl.Responses;
using Tenancy.Application.UseCases.Files.GetFileUrls;
using Tenancy.Application.UseCases.Files.GetFileUrls.Responses;
using Tenancy.Application.UseCases.Files.UploadFile;
using Tenancy.Application.UseCases.Files.UploadFile.Responses;
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

        // Operacion de tareas programadas (el despachador y sus puertos los registra AddBackgroundJobs).
        services.AddScoped<IRequestHandler<ListAutomatedTasksRequest, ListAutomatedTasksResponse>, ListAutomatedTasksHandler>();
        services.AddScoped<IRequestHandler<SetAutomatedTaskEnabledRequest, SetAutomatedTaskEnabledResponse>, SetAutomatedTaskEnabledHandler>();
        services.AddScoped<IRequestHandler<RunAutomatedTaskNowRequest, RunAutomatedTaskNowResponse>, RunAutomatedTaskNowHandler>();
        services.AddScoped<IRequestHandler<GetAutomatedTaskHistoryRequest, GetAutomatedTaskHistoryResponse>, GetAutomatedTaskHistoryHandler>();

        // Archivos del tenant (IObjectStorage e IStoredFileRegistry los registra AddObjectStorage).
        services.AddScoped<IRequestHandler<UploadFileRequest, UploadFileResponse>, UploadFileHandler>();
        services.AddScoped<IRequestHandler<GetFileUrlRequest, GetFileUrlResponse>, GetFileUrlHandler>();
        services.AddScoped<IRequestHandler<GetFileUrlsRequest, GetFileUrlsResponse>, GetFileUrlsHandler>();
        return services;
    }
}
