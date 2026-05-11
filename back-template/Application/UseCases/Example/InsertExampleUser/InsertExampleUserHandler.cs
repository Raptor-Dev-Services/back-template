using Application.Dto.Example.User;
using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.InsertExampleUser;

/// <summary>
/// Maneja el caso de uso <c>InsertExampleUser</c>: valida unicidad de correo y crea el usuario.
/// </summary>
public sealed class InsertExampleUserHandler : IRequestHandler<InsertExampleUserRequest, InsertExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="InsertExampleUserHandler"/>.
    /// </summary>
    /// <param name="repo">Repositorio de usuarios Example.</param>
    public InsertExampleUserHandler(IExampleUserRepository repo) => _repo = repo;

    /// <summary>
    /// Ejecuta la lógica del caso de uso.
    /// </summary>
    /// <param name="request">Datos del usuario a crear.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <see cref="InsertExampleUserSuccess"/> con el usuario creado, o
    /// <see cref="InsertExampleUserConflictFailure"/> si el correo ya existe.
    /// </returns>
    public async Task<InsertExampleUserResponse> Handle(InsertExampleUserRequest request, CancellationToken cancellationToken)
    {
        if (await _repo.ExistsByEmailAsync(request.Email, cancellationToken))
            return new InsertExampleUserConflictFailure("El email ya está registrado.");

        var user = await _repo.InsertAsync(
            request.FullName, request.Email, request.Department, request.Notes, cancellationToken);

        return new InsertExampleUserSuccess(new ExampleUserDto(
            user.PublicId, user.FullName, user.Email, user.Department,
            user.Notes, user.IsActive, user.CreatedAtUtc, user.UpdatedAtUtc));
    }
}
