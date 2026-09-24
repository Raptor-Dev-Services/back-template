using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;

namespace Authentication.Application.UseCases.TwoFactor.Responses;

public abstract record BeginTwoFactorSetupResponse : IResponse;

public sealed record BeginTwoFactorSetupSuccess(TwoFactorSetupDto Data) : BeginTwoFactorSetupResponse, ISuccess<TwoFactorSetupDto>;

public sealed record BeginTwoFactorSetupConflictFailure(string Message) : BeginTwoFactorSetupResponse, IConflictFailure;

public sealed record BeginTwoFactorSetupNotFoundFailure(string Message) : BeginTwoFactorSetupResponse, INotFoundFailure;

public abstract record EnableTwoFactorResponse : IResponse;

public sealed record EnableTwoFactorSuccess(RecoveryCodesDto Data) : EnableTwoFactorResponse, ISuccess<RecoveryCodesDto>;

public sealed record EnableTwoFactorValidationFailure(string Message) : EnableTwoFactorResponse, IValidationFailure;

public sealed record EnableTwoFactorConflictFailure(string Message) : EnableTwoFactorResponse, IConflictFailure;

public abstract record DisableTwoFactorResponse : IResponse;

public sealed record DisableTwoFactorSuccess(AcceptedDto Data) : DisableTwoFactorResponse, ISuccess<AcceptedDto>;

public sealed record DisableTwoFactorValidationFailure(string Message) : DisableTwoFactorResponse, IValidationFailure;
