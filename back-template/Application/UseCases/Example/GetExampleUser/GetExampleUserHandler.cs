using Application.Dto.Example.User;
using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.GetExampleUser;

/// <summary>
/// Maneja el caso de uso <c>GetExampleUser</c>: busca un usuario por su identificador público.
/// </summary>
public sealed class GetExampleUserHandler : IRequestHandler<GetExampleUserRequest, GetExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="GetExampleUserHandler"/>.
    /// </summary>
    /// <param name="repo">Repositorio de usuarios Example.</param>
    public GetExampleUserHandler(IExampleUserRepository repo) => _repo = repo;

    /// <summary>
    /// Ejecuta la lógica del caso de uso.
    /// </summary>
    /// <param name="request">Parámetros de entrada con el <c>PublicId</c> del usuario.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <see cref="GetExampleUserSuccess"/> con el DTO del usuario, o
    /// <see cref="GetExampleUserNotFoundFailure"/> si no existe.
    /// </returns>
    public async Task<GetExampleUserResponse> Handle(GetExampleUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _repo.GetByPublicIdAsync(request.PublicId, cancellationToken);
        if (user is null)
            return new GetExampleUserNotFoundFailure("Usuario no encontrado.");

        return new GetExampleUserSuccess(new ExampleUserDto(
            user.PublicId, user.FullName, user.Email, user.Department,
            user.Notes, user.IsActive, user.CreatedAtUtc, user.UpdatedAtUtc));
    }
}
