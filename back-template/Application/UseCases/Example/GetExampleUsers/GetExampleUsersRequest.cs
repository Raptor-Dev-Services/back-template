using Common.Messaging;

namespace Application.UseCases.Example.GetExampleUsers;

/// <summary>
/// Solicita una página de usuarios Example.
/// </summary>
/// <param name="Page">Número de página (base 1).</param>
/// <param name="PageSize">Cantidad de registros por página.</param>
public sealed record GetExampleUsersRequest(int Page, int PageSize) : IRequest<GetExampleUsersResponse>;
