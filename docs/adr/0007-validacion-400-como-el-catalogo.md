# ADR-0007 - Un fallo de validacion responde 400, como manda el catalogo

- Estado: Aceptado
- Fecha: 2026-09-24
- Deciden: mantenedores de back-template
- Supersede a: ADR-0006

## Contexto y problema

ADR-0006 separo 400 (peticion ilegible) de 422 (peticion bien formada que viola una regla). La regla
`backend-architecture` del catalogo, que es la fuente de verdad de esta plantilla, mapea
`IBadRequestFailure` e `IValidationFailure` a **400**, y `api-response-envelope` remite a esa tabla.
Common, sobre el que corre la plantilla, tambien responde 400 a una `BusinessRuleException`. Una
plantilla que diverge de su catalogo reparte la divergencia a cada producto que nace de ella.

## Opciones consideradas

1. Mantener 422 y proponer el cambio al catalogo.
2. Alinear la plantilla con el catalogo (400).

## Decision

Opcion 2. `IValidationFailure`, `ValidationException` y `BusinessRuleException` responden 400, igual
que `IBadRequestFailure` y el modelo malformado. El cliente distingue el caso por el `message` del
envelope, no por el status. Si el catalogo adopta la distincion 400/422, se revisa aqui con un ADR nuevo.

## Consecuencias

- Un solo status para "tu peticion no vale", como en el resto de los productos que siguen el catalogo.
- Se pierde la senal 400/422 en metricas; no la usaba nadie.

## Cumplimiento

Tabla unica en `Shared.Web/Errors/FailureStatusCodes.cs`; `BusinessExceptionFilterTests` y las pruebas de
integracion que esperaban 422 ahora esperan 400.
