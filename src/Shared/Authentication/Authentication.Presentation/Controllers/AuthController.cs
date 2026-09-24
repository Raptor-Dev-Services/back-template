using Authentication.Application.UseCases.CompleteTwoFactorLogin;
using Authentication.Application.UseCases.Login;
using Authentication.Application.UseCases.Logout;
using Authentication.Application.UseCases.RefreshSession;
using Authentication.Application.UseCases.RequestPasswordReset;
using Authentication.Application.UseCases.ResetPassword;
using Authentication.Presentation.RequestBodies;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Web;

namespace Authentication.Presentation.Controllers;

/// <summary>
/// La superficie ANONIMA de credenciales. Toda lleva la politica de tasa estricta por IP (fuerza bruta,
/// inundacion de correos). Ya no existe <c>POST /register</c>: el primer administrador de un tenant se crea con
/// el bootstrap gateado por secreto, y los demas usuarios los invita un administrador.
/// </summary>
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(IMediator mediator, ResultViewModel<AuthController> viewModel) : BaseApiController(mediator)
{
    /// <summary>Correo + contrasena -> access JWT (corto) + refresh token (rotativo).</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new LoginRequest(body.Email, body.Password, ClientIp), cancellationToken), viewModel);

    /// <summary>
    /// Segundo paso del login con 2FA: el challengeToken que devolvio el login mas un codigo TOTP o de
    /// recuperacion, a cambio de la sesion. Mismo limite de tasa estricto: 6 digitos son fuerza bruta barata.
    /// </summary>
    [HttpPost("login/2fa")]
    public async Task<IActionResult> LoginTwoFactor([FromBody] TwoFactorLoginBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new CompleteTwoFactorLoginRequest(body.ChallengeToken, body.Code, ClientIp), cancellationToken), viewModel);

    /// <summary>Rota el refresh token: revoca el presentado y emite uno nuevo con un access JWT al dia.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new RefreshSessionRequest(body.RefreshToken, ClientIp), cancellationToken), viewModel);

    /// <summary>Cierra la sesion del refresh token presentado. Idempotente.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new LogoutRequest(body.RefreshToken), cancellationToken), viewModel);

    /// <summary>Pide un enlace de restablecimiento. Respuesta IDENTICA exista o no la cuenta.</summary>
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new RequestPasswordResetRequest(body.Email), cancellationToken), viewModel);

    /// <summary>Fija la contrasena con el token del enlace (restablecimiento o invitacion). Cierra todas las sesiones.</summary>
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new ResetPasswordRequest(body.Token, body.NewPassword), cancellationToken), viewModel);
}
