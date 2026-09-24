# Tablero

Scrumban. Una tarjeta por linea; se mueve de columna, no se borra (lo hecho pasa a Hecho con su commit).

## Listo (para tomar)

- **Registro del release** (skill release-announcements) en `deploy.yml`, si el producto lo necesita.

## En curso

(vacio)

## Bloqueado / decision abierta

- **Primer tag**: `TAGS.md` dice "sin tag todavia". Lo decide una persona.

## Hecho

- 2026-09-24 Validacion en 400 como manda el catalogo (ADR-0007 reemplaza a ADR-0006).
- 2026-09-24 Proyecto `Tenancy.Tests` con 20 pruebas unitarias de los handlers de tareas y archivos.
- 2026-09-24 `UserRegisteredIntegrationEvent` eliminado: se publicaba y nadie lo consumia. El ejemplo de evento
  entre modulos con consumidor real es `UserDisabledIntegrationEvent`.
- 2026-09-24 Tarea `storage.purge-orphan-objects`: concilia el bucket contra `StoredFile` tenant por tenant, con
  margen de gracia y en seco por omision.
- 2026-09-24 Acciones de GitHub fijadas por SHA, con Dependabot para mantenerlas al dia.
- 2026-09-24 `UsersController` en `Users.Presentation.Controllers`, como el resto.
- 2026-09-24 Filas del template en el devstack (`PUERTOS.md` e init de Postgres y MinIO), en el catalogo.

- Chasis generico completo (ver CHANGELOG, commits `38c0965` .. `HEAD`), verificado con 171 pruebas y corrida en
  vivo contra el devstack (2026-09-24).
- Contrato alineado con front-template: permisos `users.read`/`users.manage` como claims `permission`, body
  `refreshToken`, paginado `items/page/pageSize/totalCount/totalPages`.
