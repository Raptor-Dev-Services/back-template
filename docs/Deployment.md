# Contenedores y despliegue

Procedimiento de produccion y secretos: [deploy/runbook.md](deploy/runbook.md) y
[deploy/github-secrets.md](deploy/github-secrets.md). Esta pagina explica las piezas.

## Imagen (`Dockerfile`)

Multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` compila; la final es `aspnet:10.0-noble-chiseled` (sin shell ni
gestor de paquetes, usuario `app`, puerto 8080). Lleva:

| Ruta en la imagen | Que es |
|---|---|
| `/app` | la API publicada |
| `/app/probe/HealthProbe.dll` | sonda .NET de `/health/ready`; es el `HEALTHCHECK` de la imagen (no hay curl/wget) |
| `/app/migrations/migrations.sql` | script **idempotente** de EF de esta version (sin BOM) |
| `/app/migrations/000_app_role_grants.sql`, `001_enable_rls.sql` | grants del rol de la app y RLS |

Sin shell no hay `cat`: los scripts se extraen con `docker create` + `docker cp`.

```sh
docker build -t back-template-api:local .
```

## Desarrollo en contenedor (`compose-dev.yaml`)

- **Contra el devstack** (normal): la API se engancha a la red externa `devstack` y usa su Postgres, MinIO, Mailpit
  y Seq. Puerto `5060`.
- **Standalone** (`--profile standalone`): agrega Postgres, MinIO y Mailpit propios.

En los dos casos la base se provisiona antes con `./scripts/dev-db.sh all` (ver `docs/DEV-STACK.md`). Para
desarrollar sigue ganando `dotnet run`.

## Produccion (`docker-compose.prod.yml` + `Caddyfile`)

- `api` (imagen `${IMAGE_NAME}:${IMAGE_TAG}`, ambos obligatorios) y `caddy` (TLS automatico, `admin off`, tope de
  cuerpo de 8 MB, cabeceras de seguridad, log sin credenciales). Solo Caddy publica puertos (80/443).
- Postgres y el almacenamiento de objetos son gestionados, fuera del compose.
- Secretos con `${VAR:?}`: si falta uno, el `up` aborta. Nombres de contenedor fijos (`back-template-api`,
  `back-template-caddy`) para diagnosticar sin re-interpolar el compose.
- Logs `json-file` rotados (10 MB x 3).

## Pipeline (`.github/workflows/`)

| Workflow | Cuando | Que hace |
|---|---|---|
| `ci.yml` | PR y push a main | build `-warnaserror`, todas las pruebas, build de la imagen; gitleaks bloqueante |
| `dependencias-estables.yml` | PR y push a main | sin prerelease ni versiones flotantes |
| `deploy.yml` | tag `vX.Y.Z` / `vX.Y.Z-beta-NN` sobre main | pruebas -> imagen a `ghcr.io/<repo>` -> migracion -> deploy |
| `_deploy-to-vps.yml` | lo llaman deploy y rollback | `pull` + `up` del tag exacto, verifica la imagen corriendo y espera `/health/ready` |
| `rollback.yml` | a mano | vuelve a un tag ya publicado (no revierte el esquema) |

La migracion corre en el servidor con el rol dueno: `migrations.sql` -> grants -> RLS -> guardia que falla si una
tabla con `TenantId` quedo sin RLS fuera de la lista de exclusiones. `migrate` y `deploy` solo corren con la
variable `AUTO_DEPLOY=true`.

Los tags los crea una persona; ningun pipeline taggea.
