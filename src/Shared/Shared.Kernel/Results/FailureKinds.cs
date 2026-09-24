using Common.Results;

namespace Shared.Kernel.Results;

// Common.Results trae IValidationFailure, INotFoundFailure e IConflictFailure. Estas completan el
// juego para que TODO fallo tipado de un caso de uso tenga su codigo HTTP, igual que las excepciones
// de Shared.Kernel.Errors: las dos vias (devolver un fallo o lanzar una excepcion) acaban en el mismo
// status y el mismo envelope. El mapeo vive en Shared.Web (FailureStatusCodes).

/// <summary>Peticion malformada o no procesable -> HTTP 400.</summary>
public interface IBadRequestFailure : IFailure;

/// <summary>Credenciales o sesion invalidas -> HTTP 401.</summary>
public interface IUnauthorizedFailure : IFailure;

/// <summary>Autenticado pero sin derecho sobre el recurso -> HTTP 403.</summary>
public interface IForbiddenFailure : IFailure;
