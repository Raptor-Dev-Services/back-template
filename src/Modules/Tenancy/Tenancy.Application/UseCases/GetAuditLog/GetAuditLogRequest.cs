using Common.Messaging;
using Tenancy.Application.UseCases.GetAuditLog.Responses;

namespace Tenancy.Application.UseCases.GetAuditLog;

/// <summary>La bitacora de acciones del tenant del token, paginada y opcionalmente filtrada por accion.</summary>
public sealed record GetAuditLogRequest(int Page, int PageSize, string? Action) : IRequest<GetAuditLogResponse>;
