-- =============================================================================
-- 000_app_role_grants.sql - privilegios del rol de la APLICACION
-- =============================================================================
-- La API corre con `backtemplate_app`: solo DML, sin DDL, sin BYPASSRLS. Este script le da lo
-- justo sobre el esquema `public`. Se ejecuta CONECTADO A LA BASE como el rol DUENO del
-- esquema (backtemplate_owner), ANTES de las migraciones:
--
--   * los GRANT de abajo cubren lo que ya exista;
--   * ALTER DEFAULT PRIVILEGES cubre lo que creen las migraciones FUTURAS. Aplica a objetos
--     creados por el rol que lo ejecuta, y por eso tiene que correrlo el dueno: si lo corre
--     `postgres`, las tablas de las migraciones (que crea el dueno) quedan sin permisos para la app.
--
-- La creacion del ROL no va aqui: en desarrollo la hace scripts/db/provision-devstack.sql y en
-- produccion el aprovisionamiento de la base (DBA o IaC), con su contrasena fuera del repo.
--
-- SIN DELETE a proposito. La regla de oro es soft delete: nada de la aplicacion borra filas
-- fisicamente (AppDbContext convierte cada Remove en una marca). Negarle DELETE al rol hace que
-- un borrado fisico accidental -un ExecuteDelete, un SQL a mano- falle en el motor en vez de
-- perder datos en silencio. Si algun dia hace falta de verdad, se concede tabla por tabla.
--
-- Al crear un producto desde la plantilla, renombra `backtemplate_app` aqui y en los scripts.
-- Idempotente.
-- =============================================================================

GRANT USAGE ON SCHEMA public TO backtemplate_app;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO backtemplate_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO backtemplate_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE ON TABLES TO backtemplate_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO backtemplate_app;
