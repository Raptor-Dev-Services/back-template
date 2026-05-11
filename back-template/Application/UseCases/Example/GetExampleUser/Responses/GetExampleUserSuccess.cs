using Application.Dto.Example.User;
using Common.Results;

namespace Application.UseCases.Example.GetExampleUser;

/// <summary>
/// Respuesta exitosa que contiene los datos del usuario solicitado.
/// </summary>
/// <param name="Data">DTO con la información del usuario.</param>
public sealed record GetExampleUserSuccess(ExampleUserDto Data) : GetExampleUserResponse, ISuccess<ExampleUserDto>;
