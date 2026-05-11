using Application.Dto.Example.User;
using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.GetExampleUsers;

/// <summary>
/// Maneja el caso de uso <c>GetExampleUsers</c>: retorna una página de usuarios.
/// </summary>
public sealed class GetExampleUsersHandler : IRequestHandler<GetExampleUsersRequest, GetExampleUsersResponse>
{
    private readonly IExampleUserRepository _repo;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="GetExampleUsersHandler"/>.
    /// </summary>
    /// <param name="repo">Repositorio de usuarios Example.</param>
    public GetExampleUsersHandler(IExampleUserRepository repo) => _repo = repo;

    /// <summary>
    /// Ejecuta la lógica del caso de uso.
    /// </summary>
    /// <param name="request">Parámetros de paginación (<c>Page</c> y <c>PageSize</c>).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <see cref="GetExampleUsersSuccess"/> con la página de usuarios y el total.
    /// </returns>
    public async Task<GetExampleUsersResponse> Handle(GetExampleUsersRequest request, CancellationToken cancellationToken)
    {
        var users = await _repo.GetPagedAsync(request.Page, request.PageSize, cancellationToken);
        var total = await _repo.GetCountAsync(cancellationToken);

        var dtos = users
            .Select(u => new ExampleUserDto(
                u.PublicId, u.FullName, u.Email, u.Department,
                u.Notes, u.IsActive, u.CreatedAtUtc, u.UpdatedAtUtc))
            .ToArray();

        return new GetExampleUsersSuccess(dtos, total, request.Page, request.PageSize);
    }
}
