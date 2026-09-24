using Common.Messaging;
using Shared.Kernel.Audit;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Context;
using Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow.Responses;

namespace Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow;

internal sealed class RunAutomatedTaskNowHandler(
    IAutomatedTaskRunner runner,
    IAutomatedTaskStore store,
    BackgroundJobsOptions options,
    ICurrentUser currentUser,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : IRequestHandler<RunAutomatedTaskNowRequest, RunAutomatedTaskNowResponse>
{
    public async Task<RunAutomatedTaskNowResponse> Handle(RunAutomatedTaskNowRequest request, CancellationToken cancellationToken)
    {
        if (!OperatorGate.Allows(options, currentUser))
            return new RunAutomatedTaskNowForbiddenFailure(OperatorGate.DeniedMessage);

        switch (await runner.RunNowAsync(request.Code, currentUser.UserId, cancellationToken))
        {
            case RunNowOutcome.UnknownTask:
                return new RunAutomatedTaskNowNotFoundFailure("Tarea programada no encontrada.");
            case RunNowOutcome.AlreadyRunning:
                return new RunAutomatedTaskNowConflictFailure("La tarea ya se esta ejecutando; espera a que termine.");
        }

        audit.Append("task.run_now", "AutomatedTask", request.Code, "Tarea programada ejecutada a mano.");
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var history = await store.GetHistoryAsync(request.Code, 1, 1, cancellationToken);
        return new RunAutomatedTaskNowSuccess(history!.Items[0]);
    }
}
