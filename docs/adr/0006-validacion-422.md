# ADR-0006 - Un fallo de validacion responde 422

- Estado: Supersedido por ADR-0007
- Fecha: 2026-09-24
- Deciden: mantenedores de back-template

## Contexto y problema

La regla generica del catalogo mapea `IValidationFailure` a 400. Esta plantilla distingue dos cosas que un
cliente trata distinto: una peticion que no se pudo leer (JSON roto, tipo equivocado: el cliente tiene un bug) y
una peticion bien formada que viola una regla (el usuario puede corregirla).

## Decision

- 400 (`IBadRequestFailure`, errores de binding): la peticion no se pudo interpretar.
- 422 (`IValidationFailure`, `ValidationException`): se entendio y no es valida.

Divergencia consciente con la regla del catalogo; si el catalogo cambia, se revisa aqui.

## Cumplimiento

Tabla unica en `Shared.Web/Errors/FailureStatusCodes.cs`; `ErrorEnvelopeTests` y `BusinessExceptionFilterTests`.
