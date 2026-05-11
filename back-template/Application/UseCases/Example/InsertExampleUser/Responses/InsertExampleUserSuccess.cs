using Application.Dto.Example.User;
using Common.Results;

namespace Application.UseCases.Example.InsertExampleUser;

/// <summary>
/// Respuesta exitosa que contiene el usuario recién creado.
/// </summary>
/// <param name="Data">DTO con la información del usuario creado.</param>
public sealed record InsertExampleUserSuccess(ExampleUserDto Data) : InsertExampleUserResponse, ISuccess<ExampleUserDto>;
