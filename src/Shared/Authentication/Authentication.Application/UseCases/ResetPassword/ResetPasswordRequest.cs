using Authentication.Application.UseCases.ResetPassword.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.ResetPassword;

/// <summary>Fija la contrasena con el token del enlace (invitacion o restablecimiento).</summary>
public sealed record ResetPasswordRequest(string Token, string NewPassword) : IRequest<ResetPasswordResponse>;
