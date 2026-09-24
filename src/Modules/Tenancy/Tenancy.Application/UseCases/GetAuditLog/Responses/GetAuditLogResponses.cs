using Common.Messaging;
using Common.Results;
using Shared.Kernel.Audit;
using Shared.Kernel.Results;

namespace Tenancy.Application.UseCases.GetAuditLog.Responses;

public abstract record GetAuditLogResponse : IResponse;

public sealed record GetAuditLogSuccess(PagedResult<AuditEntryDto> Data) : GetAuditLogResponse, ISuccess<PagedResult<AuditEntryDto>>;
