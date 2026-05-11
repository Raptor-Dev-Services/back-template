using Common.Messaging;

namespace Application.UseCases.Example.GetExampleUsers;

public sealed record GetExampleUsersRequest(int Page, int PageSize) : IRequest<GetExampleUsersResponse>;
