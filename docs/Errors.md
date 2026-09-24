# Errores y envelope

## El envelope unico

Toda respuesta de la API, exito o fallo, tiene la forma de `ResultViewModel<T>` de Common:

```json
{ "data": { ... }, "isSuccess": true, "message": null, "utcTimeStamp": "2026-09-24T17:00:00Z" }
```

Los endpoints lo llenan via su presenter. Lo que no pasa por un presenter (excepciones, modelo invalido, 429 del
limitador, 403 del guard de tenant suspendido, 401/403/404 sin cuerpo) lo arma `Shared.Web/Errors/ApiEnvelope.cs`
con el **mismo tipo**, para que el cliente conozca un solo formato.

## Dos vias, una tabla

Un caso de uso falla **devolviendo** un response con una marca, o **lanzando** una excepcion de negocio. La tabla
unica de status es `Shared.Web/Errors/FailureStatusCodes.cs`:

| Status | Marca del response | Excepcion (`Shared.Kernel/Errors/BusinessException.cs`) |
|---|---|---|
| 400 | `IBadRequestFailure` (y el modelo malformado) | `BadRequestException` |
| 401 | `IUnauthorizedFailure` | `UnauthorizedException` |
| 403 | `IForbiddenFailure` | `ForbiddenException` |
| 404 | `INotFoundFailure` | `NotFoundException` |
| 409 | `IConflictFailure` | `ConflictException` |
| 422 | `IValidationFailure` | `ValidationException` y cualquier `BusinessRuleException` de Common |
| 400 | un `IFailure` sin marca (conviene marcarlo) | - |
| 500 | - | cualquier otra excepcion |

Las marcas 400/401/403 estan en `Shared.Kernel/Results/FailureKinds.cs`; las de 404/409/422 vienen de
`Common.Results`. Todas las excepciones de negocio derivan de `BusinessRuleException` de Common.

## Quien traduce

- `BusinessExceptionFilter` (filtro MVC global): una excepcion de negocio sale con su status y su mensaje. Una
  peticion cancelada por el cliente se registra como 499 y no se escribe nada; si la respuesta ya empezo, no se toca.
- `EnvelopeExceptionHandler` (`IExceptionHandler`, via `UseApiErrorHandling`): lo que se escapa del MVC. Una
  excepcion inesperada responde **500 con mensaje generico**; el detalle (tipo, stack, inner) va al log, nunca al
  cliente.
- Las paginas de estado vacias (401 del esquema JWT, 403 de una policy, 404 de ruta) reciben el envelope con el
  mensaje generico de `ApiEnvelope.DefaultMessageFor`.

**No** hay `try/catch` en controllers ni handlers para "traducir" errores: capturar para devolver
`ex.Message` o el inner filtraria detalles internos. `ErrorEnvelopeTests` lo verifica con un controller de prueba
que lanza (`BoomController`).

## Errores que produce la persistencia

`AppDbContext` convierte, antes de que lleguen al cliente:

- violacion de indice unico (Postgres `23505`) -> `ConflictException` (409) con mensaje legible;
- perder una carrera de concurrencia optimista (`xmin`) -> `ConflictException` (409);
- insertar una fila de tenant sin tenant en contexto -> `InvalidOperationException` (500: es un bug, no un caso de
  negocio).

## Mensajes

Se escriben para quien usa la aplicacion: que paso y que puede hacer. Nunca nombres de tabla, SQL, stack ni si una
cuenta existe (login y "olvide mi contrasena" responden igual para un correo inexistente).
