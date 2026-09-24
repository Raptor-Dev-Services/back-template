using Common.Messaging;
using Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow.Responses;

namespace Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow;

/// <summary>"Ejecutar ahora": corre la tarea fuera de su horario (y aunque este pausada) y devuelve su corrida.</summary>
public sealed record RunAutomatedTaskNowRequest(string Code) : IRequest<RunAutomatedTaskNowResponse>;
