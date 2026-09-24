using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;

namespace Authentication.Application.UseCases.ResetPassword.Responses;

public abstract record ResetPasswordResponse : IResponse;

public sealed record ResetPasswordSuccess(AcceptedDto Data) : ResetPasswordResponse, ISuccess<AcceptedDto>;

/// <summary>Token inexistente, expirado o ya usado: el mismo mensaje, sin decir cual.</summary>
public sealed record ResetPasswordInvalidTokenFailure(string Message) : ResetPasswordResponse, IValidationFailure;

public sealed record ResetPasswordWeakPasswordFailure(string Message) : ResetPasswordResponse, IValidationFailure;
