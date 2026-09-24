# ADR-0002 - Aislamiento entre tenants en dos capas

- Estado: Aceptado
- Fecha: 2026-09-24
- Deciden: mantenedores de back-template

## Contexto y problema

Un solo esquema compartido por todos los tenants. Una consulta mal acotada -un `IgnoreQueryFilters`, un SQL crudo,
un `ExecuteUpdate` sin filtro- expone datos de otra empresa. El filtro de EF solo no basta.

## Opciones consideradas

1. Base por tenant: aislamiento total, operacion cara (migraciones x N, conexiones x N).
2. Esquema por tenant: mismo problema a menor escala.
3. Esquema compartido con filtro global de EF + Row-Level Security de Postgres.

## Decision

Opcion 3. El tenant sale **solo** del claim `tenant_id` del JWT. `TenantEntity` recibe el filtro con nombre
`tenant`; el interceptor fija `app.tenant_id` con `set_config` parametrizado en cada conexion; las tablas con
`TenantId` tienen `ENABLE` + `FORCE ROW LEVEL SECURITY`. La API corre con un rol sin BYPASSRLS ni DELETE y
`RlsRoleGuard` tumba el arranque si no es asi. Las tablas que se leen antes de conocer el tenant (credenciales,
tokens, RBAC) quedan fuera a proposito y listadas.

## Consecuencias

- Positivas: un bug de consulta devuelve cero filas, no datos ajenos (fail-closed).
- Negativas: toda tabla nueva exige su bloque de RLS; los procesos de plataforma deben fijar el tenant con `ITenantScope`.
- Un tenant suspendido se corta en cada peticion con `TenantStatusGuardMiddleware` (cache de 30 s).

## Cumplimiento

`RlsIsolationTests` (toda tabla con TenantId tiene RLS salvo las excluidas; IgnoreQueryFilters no cruza RLS;
conexion reciclada no arrastra tenant), `CrossTenantIsolationTests`, `RlsExclusionDriftTests` (el guardia del
deploy exime lo mismo) y el paso 4/4 de `deploy.yml`.
