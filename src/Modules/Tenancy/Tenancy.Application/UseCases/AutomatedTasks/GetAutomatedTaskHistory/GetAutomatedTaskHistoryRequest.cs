using Common.Messaging;
using Tenancy.Application.UseCases.AutomatedTasks.GetAutomatedTaskHistory.Responses;

namespace Tenancy.Application.UseCases.AutomatedTasks.GetAutomatedTaskHistory;

/// <summary>Historial de corridas de una tarea, de la mas reciente a la mas antigua.</summary>
public sealed record GetAutomatedTaskHistoryRequest(string Code, int Page, int PageSize) : IRequest<GetAutomatedTaskHistoryResponse>;
