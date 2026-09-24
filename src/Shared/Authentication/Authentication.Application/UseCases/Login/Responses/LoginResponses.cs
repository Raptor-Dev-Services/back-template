using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;
using Shared.Kernel.Results;

namespace Authentication.Application.UseCases.Login.Responses;

public abstract record LoginResponse : IResponse;

/// <summary>Sesion emitida, o -con 2FA activo- el reto del segundo paso (ver <see cref="LoginResultDto"/>).</summary>
public sealed record LoginSuccess(LoginResultDto Data) : LoginResponse, ISuccess<LoginResultDto>;

/// <summary>Correo inexistente, contrasena incorrecta o cuenta dada de baja: el MISMO mensaje para los tres.</summary>
public sealed record LoginInvalidCredentialsFailure(string Message) : LoginResponse, IUnauthorizedFailure;

/// <summary>
/// Credenciales correctas pero la cuenta esta bloqueada o su tenant suspendido. Solo se dice DESPUES de
/// verificar la contrasena: quien lo lee ya demostro ser el titular.
/// </summary>
public sealed record LoginForbiddenFailure(string Message) : LoginResponse, IForbiddenFailure;
