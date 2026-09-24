using Common.Messaging;
using Common.Results;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Results;

namespace Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow.Responses;

public abstract record RunAutomatedTaskNowResponse : IResponse;

public sealed record RunAutomatedTaskNowSuccess(AutomatedTaskRunDto Data)
    : RunAutomatedTaskNowResponse, ISuccess<AutomatedTaskRunDto>;

public sealed record RunAutomatedTaskNowNotFoundFailure(string Message) : RunAutomatedTaskNowResponse, INotFoundFailure;

public sealed record RunAutomatedTaskNowConflictFailure(string Message) : RunAutomatedTaskNowResponse, IConflictFailure;

public sealed record RunAutomatedTaskNowForbiddenFailure(string Message) : RunAutomatedTaskNowResponse, IForbiddenFailure;
