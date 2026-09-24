using Common.Messaging;
using Tenancy.Application.UseCases.AutomatedTasks.ListAutomatedTasks.Responses;

namespace Tenancy.Application.UseCases.AutomatedTasks.ListAutomatedTasks;

/// <summary>Las tareas programadas con su configuracion y su ultima corrida.</summary>
public sealed record ListAutomatedTasksRequest : IRequest<ListAutomatedTasksResponse>;
