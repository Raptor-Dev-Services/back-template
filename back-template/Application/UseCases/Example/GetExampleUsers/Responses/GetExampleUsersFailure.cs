using Common.Results;

namespace Application.UseCases.Example.GetExampleUsers;

public sealed record GetExampleUsersFailure(string Message) : GetExampleUsersResponse, IFailure;
