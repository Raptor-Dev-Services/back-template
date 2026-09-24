using Authentication.Application.UseCases.TwoFactor.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.TwoFactor;

// Autoservicio sobre la PROPIA cuenta: el usuario sale del sub del JWT, nunca del cuerpo.

/// <summary>Genera un secreto TOTP PENDIENTE (no activa nada) y la URI para el QR.</summary>
public sealed record BeginTwoFactorSetupRequest(Guid UserId) : IRequest<BeginTwoFactorSetupResponse>;

/// <summary>Confirma con un codigo de la app y ACTIVA el 2FA. Devuelve los codigos de recuperacion (una vez).</summary>
public sealed record EnableTwoFactorRequest(Guid UserId, string Code) : IRequest<EnableTwoFactorResponse>;

/// <summary>Apaga el 2FA. Exige un segundo factor vigente (TOTP o recuperacion), no solo la sesion.</summary>
public sealed record DisableTwoFactorRequest(Guid UserId, string Code) : IRequest<DisableTwoFactorResponse>;
