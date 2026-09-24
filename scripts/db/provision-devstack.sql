-- =============================================================================
-- provision-devstack.sql - base y roles de back-template en el Postgres COMPARTIDO
-- =============================================================================
-- El init del devstack (devstack/init/postgres/00-bases-y-roles.sql) solo corre cuando su
-- volumen esta vacio, asi que un producto nuevo no entra ahi sin recrear la base de TODOS.
-- Este script hace lo mismo para back-template sobre un devstack ya levantado, y es
-- IDEMPOTENTE: correrlo dos veces no cambia nada.
--
--   scripts/dev-db.sh provision
--
-- Se ejecuta con psql como superusuario (postgres), conectado a la base `postgres`.
--
-- DOS ROLES, y el reparto de BYPASSRLS va al reves de lo que parece -- a proposito:
--
--   backtemplate_owner  dueno del esquema. Aplica migraciones (DDL) y el script de RLS.
--                       CON BYPASSRLS: las policies se declaran FORCE, que aplica tambien al
--                       dueno de la tabla, y las pruebas de aislamiento necesitan sembrar filas
--                       de dos tenants distintos con el.
--   backtemplate_app    el que usa la API en runtime. Solo DML. SIN BYPASSRLS y sin
--                       superusuario: es lo que hace que RLS tenga efecto. La API se niega a
--                       arrancar si detecta lo contrario (RlsRoleGuard).
--
-- Contrasenas de DESARROLLO LOCAL, triviales a proposito: este Postgres solo escucha en la
-- maquina de desarrollo. No reutilices ninguna fuera de aqui.
-- =============================================================================

\set ON_ERROR_STOP on

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'backtemplate_owner') THEN
        CREATE ROLE backtemplate_owner LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS
            PASSWORD 'backtemplate_owner_dev';
    END IF;

    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'backtemplate_app') THEN
        CREATE ROLE backtemplate_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS
            PASSWORD 'backtemplate_app_dev';
    END IF;
END
$$;

-- CREATE DATABASE no puede ir dentro de un bloque DO (no admite transaccion): se genera la
-- sentencia solo si hace falta y psql la ejecuta con \gexec.
SELECT 'CREATE DATABASE backtemplate OWNER backtemplate_owner'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'backtemplate')
\gexec

-- Nadie mas que estos dos roles entra a la base: sin esto, CONNECT queda concedido a PUBLIC.
REVOKE ALL ON DATABASE backtemplate FROM PUBLIC;
GRANT CONNECT, TEMPORARY ON DATABASE backtemplate TO backtemplate_owner;
GRANT CONNECT ON DATABASE backtemplate TO backtemplate_app;
