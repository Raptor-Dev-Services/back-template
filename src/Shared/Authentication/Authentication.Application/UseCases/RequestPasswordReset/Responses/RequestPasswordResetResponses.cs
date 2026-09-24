using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;

namespace Authentication.Application.UseCases.RequestPasswordReset.Responses;

public abstract record RequestPasswordResetResponse : IResponse;

/// <summary>
/// La UNICA respuesta, exista o no la cuenta (anti-enumeracion): distinguir "no existe" de "te mandamos el
/// correo" convierte el endpoint en un directorio de clientes.
/// </summary>
public sealed record RequestPasswordResetAccepted(AcceptedDto Data) : RequestPasswordResetResponse, ISuccess<AcceptedDto>;
