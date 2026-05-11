using Common.Messaging;

namespace Application.UseCases.Example.UpdateExampleUser;

/// <summary>
/// Respuesta base (unión discriminada) del caso de uso <c>UpdateExampleUser</c>.
/// </summary>
public abstract record UpdateExampleUserResponse : IResponse;
