-- =============================================================================
-- 001_enable_rls.sql - Row-Level Security: la SEGUNDA barrera de aislamiento entre tenants
-- =============================================================================
-- La primera es el filtro global de EF ("tenant", por convencion sobre TenantEntity). Esta
-- vive en el MOTOR: aunque una consulta la escriba alguien con IgnoreQueryFilters(), con
-- SQL crudo o con un ExecuteUpdate mal acotado, Postgres no devuelve ni acepta filas de
-- otro tenant. Fail-closed por diseno.
--
-- COMO SE ALIMENTA. TenantRlsConnectionInterceptor fija el GUC `app.tenant_id` en CADA
-- apertura de conexion (tambien las que EF reutiliza del pool) con set_config
-- PARAMETRIZADO, desde el tenant del JWT. Sin tenant en contexto lo fija a vacio:
-- current_setting('app.tenant_id', true) devuelve '', NULLIF(...)::bigint es NULL, la
-- comparacion es falsa y no se ve ninguna fila.
--
-- QUIEN QUEDA SUJETO. Solo un rol sin superusuario y sin BYPASSRLS: la API corre con
-- `backtemplate_app` y RlsRoleGuard tumba el arranque si no es asi. FORCE aplica las
-- policies tambien al dueno de la tabla; el dueno de desarrollo tiene BYPASSRLS para poder
-- sembrar datos de varios tenants en las pruebas.
--
-- QUE TABLAS. Toda tabla de negocio con columna TenantId, SALVO las que se leen antes de
-- que exista tenant. Excluidas a proposito (y listadas en RlsIsolationTests y en el guardia
-- del pipeline de deploy):
--
--   UserCredential, RefreshToken,  el login (y su segundo paso), el refresh y el enlace de
--   PasswordSetupToken,            restablecimiento buscan por correo, id o hash sin saber aun el
--   TwoFactorRecoveryCode          tenant. Las protege el filtro de EF y que esas consultas van
--                                  por claves unicas en todo el sistema.
--   Role, RolePermission, UserRole los permisos del token se calculan en el login, antes de que
--                                  haya contexto, y el catalogo se re-siembra al arrancar. Sus
--                                  consultas re-acotan el TenantId a mano (RbacRepository).
--
-- Una tabla nueva con TenantId que no este aqui ni en la lista de excluidas hace fallar la
-- prueba TodaTablaConTenantId_TieneRlsHabilitadoYForzado y el deploy.
--
-- Se aplica DESPUES de las migraciones (una tabla nueva todavia no existe antes) y se
-- re-aplica en cada deploy. Idempotente.
-- =============================================================================

ALTER TABLE public."UserProfile" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."UserProfile" FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS userprofile_tenant_isolation ON public."UserProfile";
CREATE POLICY userprofile_tenant_isolation ON public."UserProfile"
    USING ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
    WITH CHECK ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::bigint);

-- Bitacora de acciones: cada tenant ve solo la suya, y una accion de sistema (bootstrap) entra con el tenant
-- fijado por ITenantScope.
ALTER TABLE public."AuditLog" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."AuditLog" FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS auditlog_tenant_isolation ON public."AuditLog";
CREATE POLICY auditlog_tenant_isolation ON public."AuditLog"
    USING ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
    WITH CHECK ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::bigint);

-- Registro de propiedad de los archivos: firmar una URL de lectura exige que la clave sea del tenant.
ALTER TABLE public."StoredFile" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."StoredFile" FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS storedfile_tenant_isolation ON public."StoredFile";
CREATE POLICY storedfile_tenant_isolation ON public."StoredFile"
    USING ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
    WITH CHECK ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
