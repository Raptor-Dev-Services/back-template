using Common.Messaging;
using Shared.Kernel.Audit;
using Tenancy.Application.UseCases.GetAuditLog.Responses;

namespace Tenancy.Application.UseCases.GetAuditLog;

/// <summary>El aislamiento lo aplican el filtro de EF y RLS: el lector solo ve la bitacora del tenant del token.</summary>
internal sealed class GetAuditLogHandler(IAuditLogReader reader) : IRequestHandler<GetAuditLogRequest, GetAuditLogResponse>
{
    public async Task<GetAuditLogResponse> Handle(GetAuditLogRequest request, CancellationToken cancellationToken) =>
        new GetAuditLogSuccess(await reader.GetPageAsync(request.Page, request.PageSize, request.Action, cancellationToken));
}
