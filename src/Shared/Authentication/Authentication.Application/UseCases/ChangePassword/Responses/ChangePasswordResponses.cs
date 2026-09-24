using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;

namespace Authentication.Application.UseCases.ChangePassword.Responses;

public abstract record ChangePasswordResponse : IResponse;

public sealed record ChangePasswordSuccess(AcceptedDto Data) : ChangePasswordResponse, ISuccess<AcceptedDto>;

public sealed record ChangePasswordValidationFailure(string Message) : ChangePasswordResponse, IValidationFailure;

public sealed record ChangePasswordNotFoundFailure(string Message) : ChangePasswordResponse, INotFoundFailure;
