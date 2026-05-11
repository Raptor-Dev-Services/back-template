using Common.Messaging;

namespace Application.UseCases.Example.InsertExampleUser;

/// <summary>
/// Respuesta base (unión discriminada) del caso de uso <c>InsertExampleUser</c>.
/// </summary>
public abstract record InsertExampleUserResponse : IResponse;
