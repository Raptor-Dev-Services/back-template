using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.UpdateExampleUser;

/// <summary>
/// Maneja el caso de uso <c>UpdateExampleUser</c>: actualiza los campos editables de un usuario activo.
/// </summary>
public sealed class UpdateExampleUserHandler : IRequestHandler<UpdateExampleUserRequest, UpdateExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="UpdateExampleUserHandler"/>.
    /// </summary>
    /// <param name="repo">Repositorio de usuarios Example.</param>
    public UpdateExampleUserHandler(IExampleUserRepository repo) => _repo = repo;

    /// <summary>
    /// Ejecuta la lógica del caso de uso.
    /// </summary>
    /// <param name="request">Datos de actualización con el identificador y los nuevos campos.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <see cref="UpdateExampleUserSuccess"/> si la actualización fue exitosa, o
    /// <see cref="UpdateExampleUserNotFoundFailure"/> si el usuario no existe.
    /// </returns>
    public async Task<UpdateExampleUserResponse> Handle(UpdateExampleUserRequest request, CancellationToken cancellationToken)
    {
        var updated = await _repo.UpdateAsync(
            request.PublicId, request.FullName, request.Department, request.Notes, cancellationToken);

        if (!updated)
            return new UpdateExampleUserNotFoundFailure("Usuario no encontrado.");

        return new UpdateExampleUserSuccess();
    }
}
