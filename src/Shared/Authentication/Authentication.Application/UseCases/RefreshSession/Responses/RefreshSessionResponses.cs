using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;
using Shared.Kernel.Results;

namespace Authentication.Application.UseCases.RefreshSession.Responses;

public abstract record RefreshSessionResponse : IResponse;

public sealed record RefreshSessionSuccess(AuthTokensDto Data) : RefreshSessionResponse, ISuccess<AuthTokensDto>;

public sealed record RefreshSessionInvalidFailure(string Message) : RefreshSessionResponse, IUnauthorizedFailure;

public sealed record RefreshSessionForbiddenFailure(string Message) : RefreshSessionResponse, IForbiddenFailure;
