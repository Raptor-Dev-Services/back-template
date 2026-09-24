using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;

namespace Authentication.Application.UseCases.SetUserLock.Responses;

public abstract record SetUserLockResponse : IResponse;

public sealed record SetUserLockSuccess(AcceptedDto Data) : SetUserLockResponse, ISuccess<AcceptedDto>;

public sealed record SetUserLockNotFoundFailure(string Message) : SetUserLockResponse, INotFoundFailure;

public sealed record SetUserLockValidationFailure(string Message) : SetUserLockResponse, IValidationFailure;
