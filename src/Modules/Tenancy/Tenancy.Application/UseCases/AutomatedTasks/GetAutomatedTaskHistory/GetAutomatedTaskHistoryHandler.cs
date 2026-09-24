using Common.Messaging;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Context;
using Tenancy.Application.UseCases.AutomatedTasks.GetAutomatedTaskHistory.Responses;

namespace Tenancy.Application.UseCases.AutomatedTasks.GetAutomatedTaskHistory;

internal sealed class GetAutomatedTaskHistoryHandler(IAutomatedTaskStore store, BackgroundJobsOptions options, ICurrentUser currentUser)
    : IRequestHandler<GetAutomatedTaskHistoryRequest, GetAutomatedTaskHistoryResponse>
{
    public async Task<GetAutomatedTaskHistoryResponse> Handle(GetAutomatedTaskHistoryRequest request, CancellationToken cancellationToken)
    {
        if (!OperatorGate.Allows(options, currentUser))
            return new GetAutomatedTaskHistoryForbiddenFailure(OperatorGate.DeniedMessage);

        var page = await store.GetHistoryAsync(request.Code, request.Page, request.PageSize, cancellationToken);
        return page is null
            ? new GetAutomatedTaskHistoryNotFoundFailure("Tarea programada no encontrada.")
            : new GetAutomatedTaskHistorySuccess(page);
    }
}
