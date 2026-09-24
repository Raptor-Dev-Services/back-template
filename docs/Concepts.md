# Conceptos

## Flujo de una peticion

```
HTTP
 └─ Controller (hereda BaseApiController)        arma el Request y lo despacha; nada de logica ni try/catch
     └─ DispatchAsync(request) -> IMediator (Common.Messaging)
         └─ InteractorPipeline                    corre el handler, registra request/response (secretos tapados)
             └─ Handler.Handle(request)           logica de negocio; devuelve un Response TIPADO
         └─ Publish(response)                     el pipeline publica el response...
             └─ Presenter (INotificationHandler)  ...y el presenter lo vuelca en ResultViewModel<TController>
 └─ MapResult(response, viewModel)                200 si es exito; si es fallo, el status de FailureStatusCodes
HTTP { data, isSuccess, message, utcTimeStamp }
```

- El mediador es el de **Common** (`Common.Messaging.IMediator`), nunca MediatR.
- **Nada se auto-descubre**: `AddMediator()` se llama sin ensamblados y cada modulo registra a mano sus handlers
  (`{Modulo}.Application/ServiceCollectionEx.cs`) y sus presenters (`{Modulo}.Presentation/ServiceCollectionEx.cs`).
  Un registro que falta lo detecta `ValidateOnBuild` al arrancar, no el primer usuario.
- Toda respuesta pasa por el envelope `ResultViewModel<T>` de Common. Ver [Errors.md](Errors.md).

## Las piezas de un caso de uso

| Pieza | Donde | Forma |
|---|---|---|
| Request | `Application/UseCases/{Caso}/{Caso}Request.cs` | `sealed record : IRequest<{Caso}Response>` |
| Handler | `.../{Caso}Handler.cs` | `internal sealed class : IRequestHandler<,>` |
| Responses | `.../Responses/{Caso}Response.cs` | `abstract record : IResponse` + `Success : ISuccess<T>` + fallos tipados |
| Presenter | `Presentation/...` | `sealed class : ResultPresenter<TController, TResponse, TData>` |
| Controller | `Presentation/Controllers/` | hereda `BaseApiController`, un `[Authorize(Policy = ...)]` por accion |

Un fallo se expresa de dos maneras equivalentes: **devolver** un response que implementa una marca
(`IValidationFailure`, `INotFoundFailure`, `IConflictFailure`, `IBadRequestFailure`, `IUnauthorizedFailure`,
`IForbiddenFailure`) o **lanzar** una `BusinessException` de `Shared.Kernel.Errors`. Las dos acaban en el mismo
status y el mismo envelope.

## Transacciones

`IUnitOfWork.ExecuteAsync` abre una transaccion; `SaveChangesAsync` persiste. La bitacora (`IAuditLog.Append`) y
los eventos de integracion (`IMediator.Publish`) se agregan DENTRO de esa transaccion: si la accion falla, no queda
rastro de que ocurrio. Ejemplo real: `DisableUserProfileHandler`.

## Comunicacion entre modulos

Un modulo solo conoce los `Contracts` de otro: una interfaz de API (`ITenancyApi`) o un evento de integracion
publicado por el mediador (`UserShouldBeCreatedIntegrationEvent`, `UserDisabledIntegrationEvent`). Ver
[Modules.md](Modules.md).

## Tenant, usuario y UTC

- El tenant sale **solo** del claim `tenant_id` del JWT validado ([MultiTenancy.md](MultiTenancy.md)).
- El usuario de la peticion es `ICurrentUser` ([CurrentUser.md](CurrentUser.md)).
- Toda fecha es UTC de punta a punta: `timestamptz` en la base, `DateTime` con `Kind=Utc`, ISO-8601 con `Z` en el
  JSON (`Shared.Web/UtcDateTime.cs`).
