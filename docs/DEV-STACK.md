# Entorno de desarrollo

back-template no levanta su propia infraestructura: usa el **devstack** compartido de la maquina (un solo
Postgres, MinIO, Mailpit, Redis y Seq para todos los productos). Esta pagina es la parte que le toca a este repo.

## Lo que usa del devstack

| Servicio | Donde | Que usa back-template |
|---|---|---|
| API | `http://localhost:5060` | puerto propio (`dotnet run` y `compose-dev.yaml`) |
| Postgres | `localhost:5432` | base `backtemplate`, roles `backtemplate_owner` y `backtemplate_app` |
| MinIO | `localhost:9000` (consola 9001) | bucket privado `backtemplate` (la API lo crea en desarrollo si falta) |
| Mailpit | SMTP `localhost:1025`, bandeja `http://localhost:8025` | invitaciones y restablecimientos |
| Seq | `http://localhost:5341` | logs estructurados |

Antes de dar por buena una respuesta de `/health/ready`, mira los NOMBRES de los checks: este repo reporta
`postgres` y `object-storage`. Otra API en el mismo puerto responde un JSON sano parecido.

## Base de datos

```bash
./scripts/dev-db.sh all       # roles + base + permisos + migraciones + RLS (idempotente)
./scripts/dev-db.sh status    # roles, migraciones aplicadas, tablas con RLS
./scripts/dev-db.sh reset     # BORRA solo la base backtemplate y la rehace
```

Por que dos roles:

- `backtemplate_owner`: dueno del esquema. Corre migraciones y el script de RLS. En desarrollo tiene BYPASSRLS
  para poder sembrar datos de varios tenants.
- `backtemplate_app`: la API. Solo SELECT/INSERT/UPDATE (sin DELETE: el borrado es soft delete) y **NOBYPASSRLS**.
  `RlsRoleGuard` tumba el arranque si el rol puede saltarse RLS, porque con uno asi las policies se ignoran sin
  ningun sintoma.

El script de provisioning es `scripts/db/provision-devstack.sql`; el script usa `docker exec` sobre el
contenedor `devstack-postgres` (cambialo con `PG_CONTAINER`). No toca ninguna otra base.

## Correr la API

```bash
cp .env.example .env    # rellena Jwt__Key, Totp__EncryptionKey y Bootstrap__Secret (32+ bytes cada uno)
dotnet run --project src/Host/Host.Api
```

En Development la API carga `.env` (DotNetEnv); fuera de Development solo lee variables de entorno.

## En contenedor

```bash
docker compose -f compose-dev.yaml up -d --build     # contra la red externa `devstack`
docker compose -f compose-dev.yaml down
```

Sin devstack (clon suelto): `docker network create devstack` una vez y
`docker compose -f compose-dev.yaml --profile standalone up -d --build`, que agrega Postgres, MinIO y Mailpit
propios (mismos puertos: chocan con el devstack si esta arriba). Luego `PG_CONTAINER=back-template-postgres
./scripts/dev-db.sh all`.

En contenedor, la API habla con MinIO por `minio:9000` pero **firma** las URLs con `localhost:9000`
(`ObjectStorage__PublicEndpoint`): la firma S3 incluye el host y el navegador no resuelve `minio`.

## Lo que el devstack ya sabe de este repo

Desde 2026-09-24 el devstack lista a back-template en `devstack/PUERTOS.md` (API `5060`, web `5179`, base
`backtemplate`, rol `backtemplate_app`, bucket `backtemplate`) y su init crea los dos roles, la base y el bucket
en un volumen nuevo. En un devstack que ya estaba levantado antes de esa fecha, el init no vuelve a correr: ahi
lo sigue creando `./scripts/dev-db.sh provision`, que es idempotente y convive con el init.

**Un producto que nace de esta plantilla cambia puerto, base, roles y bucket en su primer commit** y agrega su
propia fila al devstack; si se queda con los de la plantilla, choca con ella la primera vez que se levanten juntas.
