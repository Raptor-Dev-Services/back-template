using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.DisableExampleUser;

/// <summary>
/// Maneja el caso de uso <c>DisableExampleUser</c>: desactiva (soft-delete) un usuario activo.
/// </summary>
public sealed class DisableExampleUserHandler : IRequestHandler<DisableExampleUserRequest, DisableExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="DisableExampleUserHandler"/>.
    /// </summary>
    /// <param name="repo">Repositorio de usuarios Example.</param>
    public DisableExampleUserHandler(IExampleUserRepository repo) => _repo = repo;

    /// <summary>
    /// Ejecuta la lógica del caso de uso.
    /// </summary>
    /// <param name="request">Identificador del usuario a desactivar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <see cref="DisableExampleUserSuccess"/> si la desactivación fue exitosa, o
    /// <see cref="DisableExampleUserNotFoundFailure"/> si el usuario no existe.
    /// </returns>
    public async Task<DisableExampleUserResponse> Handle(DisableExampleUserRequest request, CancellationToken cancellationToken)
    {
        var disabled = await _repo.DisableAsync(request.PublicId, cancellationToken);
        if (!disabled)
            return new DisableExampleUserNotFoundFailure("Usuario no encontrado.");

        return new DisableExampleUserSuccess();
    }
}
