using Authentication.Application.UseCases.ChangePassword.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.ChangePassword;

/// <summary>Cambio de la PROPIA contrasena. El usuario sale del JWT (sub), nunca del cuerpo.</summary>
public sealed record ChangePasswordRequest(Guid UserId, string CurrentPassword, string NewPassword)
    : IRequest<ChangePasswordResponse>;
