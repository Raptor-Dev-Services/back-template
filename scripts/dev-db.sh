#!/usr/bin/env bash
# Base de datos de desarrollo de back-template sobre el Postgres COMPARTIDO del devstack.
#
#   scripts/dev-db.sh provision   crea roles y base (idempotente) y da permisos a la app
#   scripts/dev-db.sh migrate     aplica las migraciones EF como el rol DUENO
#   scripts/dev-db.sh rls         aplica (o re-aplica) las policies de RLS como el dueno
#   scripts/dev-db.sh all         las tres, en orden
#   scripts/dev-db.sh status      roles, migraciones aplicadas y tablas con RLS
#
# Por que un script y no el init del devstack: ese init solo corre con el volumen vacio, asi que
# un producto nuevo no entra sin recrear la base de TODOS los productos de la maquina.
#
# Por que dos roles: la API corre con `backtemplate_app` (solo DML, sin BYPASSRLS), que es lo que
# hace que RLS tenga efecto; las migraciones y el script de RLS corren con `backtemplate_owner`.
# Ver docs/DEV-STACK.md.
#
# Variables (todas con valor por omision para el devstack):
#   PG_CONTAINER  contenedor de Postgres        (devstack-postgres; en modo standalone: backtemplate-postgres)
#   PG_HOST/PG_PORT  donde lo alcanza `dotnet ef` desde esta maquina (localhost / 5432)
#
# Ninguna credencial de aqui es un secreto: son las de desarrollo local, versionadas igual que el
# init del devstack.
set -euo pipefail
cd "$(dirname "$0")/.."

PG_CONTAINER="${PG_CONTAINER:-devstack-postgres}"
PG_HOST="${PG_HOST:-localhost}"
PG_PORT="${PG_PORT:-5432}"
DB_NAME="backtemplate"
OWNER="backtemplate_owner"
OWNER_PASSWORD="backtemplate_owner_dev"
SQL_DIR="src/Shared/Shared.Infrastructure/Persistence/Sql"

# psql DENTRO del contenedor: no hace falta tener el cliente instalado. Por el socket local del
# contenedor, que la imagen oficial de Postgres deja en `trust`.
psql_as() { # $1 rol, $2 base; el SQL por stdin
  docker exec -i "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -q -U "$1" -d "$2" -f -
}

require_container() {
  if ! docker ps --format '{{.Names}}' | grep -qx "$PG_CONTAINER"; then
    echo "No esta corriendo el contenedor '$PG_CONTAINER'." >&2
    echo "Levanta el devstack (docker compose -f <devstack>/compose-dev.yaml up -d) o, sin devstack," >&2
    echo "  docker compose -f compose-dev.yaml --profile standalone up -d  y  PG_CONTAINER=backtemplate-postgres" >&2
    exit 1
  fi
}

provision() {
  require_container
  echo "== provision: roles y base '$DB_NAME' (idempotente)"
  psql_as postgres postgres < scripts/db/provision-devstack.sql
  echo "== provision: permisos del rol de la aplicacion (como $OWNER)"
  psql_as "$OWNER" "$DB_NAME" < "$SQL_DIR/000_app_role_grants.sql"
}

migrate() {
  echo "== migrate: migraciones EF como $OWNER"
  dotnet tool restore > /dev/null
  ConnectionStrings__Migrations="Host=$PG_HOST;Port=$PG_PORT;Database=$DB_NAME;Username=$OWNER;Password=$OWNER_PASSWORD" \
    dotnet ef database update -p src/Shared/Shared.Infrastructure -s src/Host/Host.Api
}

rls() {
  require_container
  if [ ! -f "$SQL_DIR/001_enable_rls.sql" ]; then
    echo "== rls: no hay script de RLS todavia"
    return
  fi
  echo "== rls: policies de Row-Level Security (idempotente)"
  psql_as "$OWNER" "$DB_NAME" < "$SQL_DIR/001_enable_rls.sql"
}

status() {
  require_container
  psql_as postgres "$DB_NAME" <<'SQL'
\pset footer off
SELECT rolname AS rol, rolsuper AS superusuario, rolbypassrls AS bypassrls
FROM pg_roles WHERE rolname LIKE 'backtemplate%' ORDER BY 1;
SELECT count(*) AS migraciones_aplicadas FROM "__EFMigrationsHistory";
SELECT c.relname AS tabla_con_rls FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND c.relkind = 'r' AND c.relrowsecurity ORDER BY 1;
SQL
}

case "${1:-}" in
  provision) provision ;;
  migrate)   migrate ;;
  rls)       rls ;;
  all)       provision; migrate; rls ;;
  status)    status ;;
  *) sed -n '2,9p' "$0"; exit 1 ;;
esac
