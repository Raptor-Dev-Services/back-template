using Authentication.Application.UseCases.GetMyAccount.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.GetMyAccount;

/// <summary>La cuenta PROPIA, con roles y permisos leidos de la base (no del token, que puede ir atrasado).</summary>
public sealed record GetMyAccountRequest(Guid UserId) : IRequest<GetMyAccountResponse>;
