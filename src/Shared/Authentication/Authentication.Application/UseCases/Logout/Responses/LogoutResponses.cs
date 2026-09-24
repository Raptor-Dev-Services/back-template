using Authentication.Application.Dto;
using Common.Messaging;
using Common.Results;

namespace Authentication.Application.UseCases.Logout.Responses;

public abstract record LogoutResponse : IResponse;

/// <summary>Siempre exito: cerrar una sesion que ya no existe no es un error (idempotente).</summary>
public sealed record LogoutSuccess(AcceptedDto Data) : LogoutResponse, ISuccess<AcceptedDto>;
