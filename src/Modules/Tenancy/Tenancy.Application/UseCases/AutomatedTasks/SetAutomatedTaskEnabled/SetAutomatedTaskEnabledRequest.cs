using Common.Messaging;
using Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled.Responses;

namespace Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled;

/// <summary>Pausar o reanudar una tarea sin desplegar.</summary>
public sealed record SetAutomatedTaskEnabledRequest(string Code, bool IsEnabled) : IRequest<SetAutomatedTaskEnabledResponse>;
