using Common.Messaging;
using Shared.Kernel.Audit;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Context;
using Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled.Responses;

namespace Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled;

internal sealed class SetAutomatedTaskEnabledHandler(
    IAutomatedTaskStore store,
    BackgroundJobsOptions options,
    ICurrentUser currentUser,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : IRequestHandler<SetAutomatedTaskEnabledRequest, SetAutomatedTaskEnabledResponse>
{
    public async Task<SetAutomatedTaskEnabledResponse> Handle(SetAutomatedTaskEnabledRequest request, CancellationToken cancellationToken)
    {
        if (!OperatorGate.Allows(options, currentUser))
            return new SetAutomatedTaskEnabledForbiddenFailure(OperatorGate.DeniedMessage);

        if (!await store.SetEnabledAsync(request.Code, request.IsEnabled, cancellationToken))
            return new SetAutomatedTaskEnabledNotFoundFailure("Tarea programada no encontrada.");

        audit.Append(request.IsEnabled ? "task.resumed" : "task.paused", "AutomatedTask", request.Code,
            request.IsEnabled ? "Tarea programada reanudada." : "Tarea programada pausada.");
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var status = (await store.ListAsync(cancellationToken)).Single(t => t.Code == request.Code);
        return new SetAutomatedTaskEnabledSuccess(status);
    }
}
