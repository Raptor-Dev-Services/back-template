using Application.Dto.Example.User;
using Common.Results;

namespace Application.UseCases.Example.GetExampleUsers;

/// <summary>
/// Respuesta exitosa con la página de usuarios y metadatos de paginación.
/// Implementa <see cref="ISuccess"/> (sin genérico) porque contiene múltiples campos;
/// el presenter usa <c>_viewModel.OK(success)</c> para serializar el objeto completo.
/// </summary>
/// <param name="Users">Colección de usuarios de la página actual.</param>
/// <param name="Total">Total de usuarios en la base de datos.</param>
/// <param name="Page">Número de página retornado.</param>
/// <param name="PageSize">Tamaño de página usado en la consulta.</param>
public sealed record GetExampleUsersSuccess(
    IReadOnlyCollection<ExampleUserDto> Users,
    int Total,
    int Page,
    int PageSize) : GetExampleUsersResponse, ISuccess;
