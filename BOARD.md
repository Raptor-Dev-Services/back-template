# Tablero

Scrumban. Una tarjeta por linea; se mueve de columna, no se borra (lo hecho pasa a Hecho con su commit).

## Listo (para tomar)

- **Tenancy.Tests**: el proyecto no existe (solo quedan bin/obj viejos) aunque `Tenancy.Application` declara
  `InternalsVisibleTo`. Los handlers de tareas y archivos solo tienen pruebas de integracion; agregar unitarias.
- **Consumidor o baja de `UserRegisteredIntegrationEvent`**: se publica y nadie lo consume.
- **Purga de objetos huerfanos**: si `DeleteAsync` falla tras un registro fallido, el objeto queda en el bucket
  sin fila. Tarea programada que concilie prefijo de tenant contra `StoredFile`.
- **Acciones de GitHub fijadas por SHA** (hoy por etiqueta mayor `@v4`, mutable).
- **Registro del release** (skill release-announcements) en `deploy.yml`, si el producto lo necesita.

## En curso

(vacio)

## Bloqueado / decision abierta

- **Validacion 422 vs 400**: la plantilla responde 422 a `IValidationFailure` (ADR-0006); la regla generica del
  catalogo dice 400. Decidir si el catalogo adopta la distincion o la plantilla se alinea.
- **Filas del devstack**: agregar back-template a `devstack/PUERTOS.md` (y opcionalmente al init de Postgres y
  MinIO). Este repo no edita el devstack. Detalle en `docs/DEV-STACK.md`.
- **Primer tag**: `TAGS.md` dice "sin tag todavia". Lo decide una persona.

## Hecho

- Chasis generico completo (ver CHANGELOG, commits `38c0965` .. `HEAD`), verificado con 171 pruebas y corrida en
  vivo contra el devstack (2026-09-24).
- Contrato alineado con front-template: permisos `users.read`/`users.manage` como claims `permission`, body
  `refreshToken`, paginado `items/page/pageSize/totalCount/totalPages`.
