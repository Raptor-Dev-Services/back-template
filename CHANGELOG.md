# Changelog

Formato [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/). Versionado semantico por tag (`vX.Y.Z`).

## [Sin publicar] - chasis generico

Todavia sin tag. Reescritura de la plantilla como chasis de SaaS multi-tenant. **Rompe** con los tags
anteriores (`monolito-modular-v1.0.0`, `clean-arch-dapper-single-tenant-v1.0.0`): rutas, esquema y contrato
de autenticacion cambian.

### Agregado
- Estructura con `src/` y `tests/`, `Directory.Build.props`/`Directory.Packages.props` (CPM), SDK fijado en `global.json`.
- Errores: excepciones de negocio tipadas, `BusinessExceptionFilter`, un solo envelope, 500 sin detalle.
- Persistencia: `TenantEntity` (auditoria, soft delete, `xmin`), UTC obligatorio, migraciones EF versionadas.
- Aislamiento: RLS de Postgres con `app.tenant_id`, rol de app sin BYPASSRLS ni DELETE, `RlsRoleGuard`.
- Correo transversal (SMTP / log en desarrollo).
- Auth: bootstrap de tenant con secreto, invitaciones, restablecimiento, RBAC por permisos (`permission`
  claims), refresh tokens hasheados con rotacion y deteccion de reuso, bloqueo de cuentas, TOTP + codigos.
- Borde: rate limit (`auth`, `upload`, global), cabeceras de seguridad, CORS por configuracion, proxies de confianza.
- Configuracion: `.env` solo en Development, fail-fast de produccion.
- Salud: `/health/live` y `/health/ready` (Postgres, almacenamiento de objetos).
- Observabilidad: Serilog JSON, Seq, correlation id, OTLP opcional.
- Bitacora de acciones del tenant (`/api/v1/audit-log`).
- Tareas programadas con despachador unico, reclamo atomico y operacion reservada al tenant operador.
- Guard de tenant suspendido para tokens ya emitidos.
- Almacenamiento de objetos (MinIO/S3) con propiedad por tenant y firma de bytes.
- Imagen chiseled con sonda .NET y esquema de su version; compose de desarrollo contra el devstack;
  compose de produccion con Caddy.
- CI (build 0/0, pruebas con Postgres real, imagen, gitleaks), deploy por tag, rollback, puerta de dependencias.

### Quitado
- `POST /api/auth/register` anonimo, `EnsureCreated`, Prometheus `/metrics`, `/api/health`, los compose que
  duplicaban Postgres/Seq, `try/catch` que devolvian el mensaje de la excepcion.
