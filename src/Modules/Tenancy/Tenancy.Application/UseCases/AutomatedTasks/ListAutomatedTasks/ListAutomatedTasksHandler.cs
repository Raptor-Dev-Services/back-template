using Common.Messaging;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Context;
using Tenancy.Application.UseCases.AutomatedTasks.ListAutomatedTasks.Responses;

namespace Tenancy.Application.UseCases.AutomatedTasks.ListAutomatedTasks;

internal sealed class ListAutomatedTasksHandler(IAutomatedTaskStore store, BackgroundJobsOptions options, ICurrentUser currentUser)
    : IRequestHandler<ListAutomatedTasksRequest, ListAutomatedTasksResponse>
{
    public async Task<ListAutomatedTasksResponse> Handle(ListAutomatedTasksRequest request, CancellationToken cancellationToken)
    {
        if (!OperatorGate.Allows(options, currentUser))
            return new ListAutomatedTasksForbiddenFailure(OperatorGate.DeniedMessage);

        return new ListAutomatedTasksSuccess(await store.ListAsync(cancellationToken));
    }
}
