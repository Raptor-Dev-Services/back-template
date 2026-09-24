using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;
using Shared.Kernel.Results;

namespace Authentication.Application.UseCases.CompleteTwoFactorLogin.Responses;

public abstract record CompleteTwoFactorLoginResponse : IResponse;

public sealed record CompleteTwoFactorLoginSuccess(LoginResultDto Data) : CompleteTwoFactorLoginResponse, ISuccess<LoginResultDto>;

/// <summary>Reto invalido o expirado, o codigo incorrecto: el mismo mensaje, sin decir cual fallo.</summary>
public sealed record CompleteTwoFactorLoginInvalidFailure(string Message) : CompleteTwoFactorLoginResponse, IUnauthorizedFailure;

public sealed record CompleteTwoFactorLoginForbiddenFailure(string Message) : CompleteTwoFactorLoginResponse, IForbiddenFailure;
