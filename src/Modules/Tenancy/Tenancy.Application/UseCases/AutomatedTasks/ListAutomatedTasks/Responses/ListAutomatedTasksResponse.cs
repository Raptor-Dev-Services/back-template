using Common.Messaging;
using Common.Results;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Results;

namespace Tenancy.Application.UseCases.AutomatedTasks.ListAutomatedTasks.Responses;

public abstract record ListAutomatedTasksResponse : IResponse;

public sealed record ListAutomatedTasksSuccess(IReadOnlyList<AutomatedTaskStatusDto> Data)
    : ListAutomatedTasksResponse, ISuccess<IReadOnlyList<AutomatedTaskStatusDto>>;

public sealed record ListAutomatedTasksForbiddenFailure(string Message) : ListAutomatedTasksResponse, IForbiddenFailure;
