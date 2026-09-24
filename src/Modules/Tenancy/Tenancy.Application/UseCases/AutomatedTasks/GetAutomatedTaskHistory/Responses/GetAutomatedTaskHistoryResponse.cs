using Common.Messaging;
using Common.Results;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Results;

namespace Tenancy.Application.UseCases.AutomatedTasks.GetAutomatedTaskHistory.Responses;

public abstract record GetAutomatedTaskHistoryResponse : IResponse;

public sealed record GetAutomatedTaskHistorySuccess(PagedResult<AutomatedTaskRunDto> Data)
    : GetAutomatedTaskHistoryResponse, ISuccess<PagedResult<AutomatedTaskRunDto>>;

public sealed record GetAutomatedTaskHistoryNotFoundFailure(string Message) : GetAutomatedTaskHistoryResponse, INotFoundFailure;

public sealed record GetAutomatedTaskHistoryForbiddenFailure(string Message) : GetAutomatedTaskHistoryResponse, IForbiddenFailure;
