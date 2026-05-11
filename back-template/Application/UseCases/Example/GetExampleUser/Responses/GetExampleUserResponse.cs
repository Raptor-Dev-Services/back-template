using Common.Messaging;

namespace Application.UseCases.Example.GetExampleUser;

/// <summary>
/// Respuesta base (unión discriminada) del caso de uso <c>GetExampleUser</c>.
/// </summary>
public abstract record GetExampleUserResponse : IResponse;
