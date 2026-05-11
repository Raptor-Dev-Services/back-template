using Common.Messaging;

namespace Application.UseCases.Example.DisableExampleUser;

/// <summary>
/// Respuesta base (unión discriminada) del caso de uso <c>DisableExampleUser</c>.
/// </summary>
public abstract record DisableExampleUserResponse : IResponse;
