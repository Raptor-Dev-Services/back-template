using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;
using Shared.Kernel.Results;

namespace Authentication.Application.UseCases.InviteUser.Responses;

public abstract record InviteUserResponse : IResponse;

public sealed record InviteUserSuccess(InvitedUserDto Data) : InviteUserResponse, ISuccess<InvitedUserDto>;

public sealed record InviteUserValidationFailure(string Message) : InviteUserResponse, IValidationFailure;

public sealed record InviteUserConflictFailure(string Message) : InviteUserResponse, IConflictFailure;

/// <summary>Anti-escalada: solo quien ya es administrador concede el rol de administrador.</summary>
public sealed record InviteUserForbiddenFailure(string Message) : InviteUserResponse, IForbiddenFailure;
