# Multi-tenancy

Tenant = la organizacion cliente. Un tenant no puede leer ni escribir datos de otro, y eso se garantiza con **dos
barreras independientes**.

## De donde sale el tenant

**Solo** del claim `tenant_id` del JWT ya validado. Nunca del body, del query string ni de un header que controle
el cliente. `Shared.Web/Tenancy/TenantContextMiddleware.cs` (despues de `UseAuthentication`) lo publica en el
`ITenantContextAccessor` de Common y lo limpia al terminar la peticion. Una peticion anonima no tiene tenant.

## Barrera 1: filtro de EF

Toda entidad `TenantEntity` recibe el filtro con nombre `tenant`. Los repositorios **no** repiten
`WHERE TenantId = ...`. Solo la autenticacion lo ignora (`IgnoreQueryFilters([QueryFilterNames.Tenant])`), porque
login, refresh y restablecimiento buscan por correo, id o hash antes de conocer el tenant.

## Barrera 2: RLS en Postgres

`TenantRlsConnectionInterceptor` fija el GUC `app.tenant_id` en cada apertura de conexion (tambien las del pool)
con `set_config` **parametrizado**. Sin tenant lo deja vacio y ninguna policy deja pasar filas (fail-closed).
`001_enable_rls.sql` hace `ENABLE` + `FORCE ROW LEVEL SECURITY` y una policy `USING`/`WITH CHECK` sobre
`"TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::bigint` para `UserProfile`, `AuditLog` y
`StoredFile`.

Asi, aunque alguien escriba `IgnoreQueryFilters()`, SQL crudo o un `ExecuteUpdate` mal acotado, el motor no
devuelve ni acepta filas de otro tenant. Solo aplica a un rol sin `BYPASSRLS`, y `RlsRoleGuard` lo garantiza.

**Excluidas a proposito** (mismo listado en el encabezado del script, en `RlsIsolationTests.ExcludedOnPurpose` y
en el guardia de `deploy.yml`; `RlsExclusionDriftTests` falla si divergen): `UserCredential`, `RefreshToken`,
`PasswordSetupToken`, `TwoFactorRecoveryCode`, `Role`, `RolePermission`, `UserRole`.

## Acciones de sistema con tenant explicito

Cuando la accion no llega con el tenant en el token (el bootstrap crea el tenant en la misma peticion), `ITenantScope.Enter`
(`EfUnitOfWork.cs`) fija el tenant en el accessor y sincroniza el GUC de la conexion abierta, dentro de un bloque
acotado.

## Tenant suspendido

`Tenant.Status` es `Active` o `Suspended`. Login, 2FA y refresh rechazan un tenant inactivo, pero un access token ya
emitido seguiria valido hasta vencer. `TenantStatusGuardMiddleware` (despues de `TenantContextMiddleware`) corta con
403 y el envelope toda peticion autenticada de un tenant suspendido o borrado. El estado lo da
`ITenantStatusProvider` (Tenancy.Infrastructure) con cache de 30 s por replica.

## Lo que se verifica

- `RlsIsolationTests`: con el tenant fijado solo se ven sus filas, sin tenant nada, no se escribe una fila ajena,
  `IgnoreQueryFilters` cruza EF pero no RLS, una conexion reciclada no arrastra el tenant anterior, y toda tabla con
  `TenantId` tiene RLS o esta en la lista de exclusiones.
- `CrossTenantIsolationTests`: por HTTP, un tenant no lista, no lee ni confirma la existencia de datos de otro
  (404, no 403).
- `SuspendedTenantTests`: token vigente de un tenant suspendido -> 403.

## Plataforma frente a tenant

Hay cosas de TODA la plataforma, no de un tenant: las tareas programadas. Operarlas exige `tasks.manage` **y**
pertenecer al tenant operador (`BackgroundJobs:OperatorTenantId`); sin configurarlo, nadie las opera desde la API.
