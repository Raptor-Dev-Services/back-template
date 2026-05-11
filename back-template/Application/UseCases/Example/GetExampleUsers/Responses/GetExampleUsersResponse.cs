using Common.Messaging;

namespace Application.UseCases.Example.GetExampleUsers;

/// <summary>
/// Respuesta base (unión discriminada) del caso de uso <c>GetExampleUsers</c>.
/// </summary>
public abstract record GetExampleUsersResponse : IResponse;
