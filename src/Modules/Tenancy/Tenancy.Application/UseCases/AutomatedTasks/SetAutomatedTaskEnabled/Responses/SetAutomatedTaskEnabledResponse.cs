using Common.Messaging;
using Common.Results;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Results;

namespace Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled.Responses;

public abstract record SetAutomatedTaskEnabledResponse : IResponse;

public sealed record SetAutomatedTaskEnabledSuccess(AutomatedTaskStatusDto Data)
    : SetAutomatedTaskEnabledResponse, ISuccess<AutomatedTaskStatusDto>;

public sealed record SetAutomatedTaskEnabledNotFoundFailure(string Message) : SetAutomatedTaskEnabledResponse, INotFoundFailure;

public sealed record SetAutomatedTaskEnabledForbiddenFailure(string Message) : SetAutomatedTaskEnabledResponse, IForbiddenFailure;
