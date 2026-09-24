# Runbook de produccion

## Preparar el servidor (una vez)

1. Docker + plugin compose; un usuario de despliegue en el grupo `docker`.
2. `mkdir -p /opt/back-template` y copiar ahi **solo** `docker-compose.prod.yml` y `Caddyfile`.
3. Si la imagen es privada: `docker login ghcr.io` con un token `read:packages`.
4. DNS: registro A de `API_DOMAIN` a la IP del servidor (solo-DNS si hay CDN).
5. Base: crear roles y base con el equivalente de `scripts/db/provision-devstack.sql` (contrasenas reales,
   `backtemplate_owner` dueno del esquema, `backtemplate_app` **NOBYPASSRLS**). En una Postgres gestionada el
   dueno no suele poder tener BYPASSRLS; no lo necesita en produccion.
6. Secretos y variables en GitHub: [github-secrets.md](github-secrets.md). Encender con `AUTO_DEPLOY=true`.

## Desplegar

Empujar un tag `vX.Y.Z` (o `vX.Y.Z-beta-NN`) sobre un commit de `main`. El pipeline prueba, publica la imagen,
migra (esquema idempotente + grants + RLS + guardia de tablas sin RLS) y despliega; no termina en verde hasta que
`/health/ready` responde sano. Los tags los crea una persona: el pipeline nunca taggea.

## Diagnosticar (sin variables)

Los contenedores tienen nombre fijo; nada de esto re-interpola el compose:

```sh
docker ps                                   # estado y (healthy)/(unhealthy)
docker logs --tail 200 back-template-api
docker exec back-template-api dotnet /app/probe/HealthProbe.dll   # 200 = listo
docker restart back-template-api            # conserva el env con el que se desplego
```

**Nunca** `docker compose up` a mano: los secretos solo existen en GitHub y el `up` abortaria.

## Revertir

Actions -> **Rollback** -> version exacta ya publicada + motivo. Usa el mismo codigo que el deploy y espera a
`/health/ready`. **No revierte el esquema**: si el release traia una migracion no retrocompatible, arreglar
hacia adelante o restaurar un respaldo (perdiendo lo escrito desde entonces). Por eso las migraciones se
escriben expand/contract.

## Primer tenant

Con `Bootstrap__Secret` configurado:

```sh
curl -X POST https://$API_DOMAIN/api/v1/bootstrap/tenant \
  -H "X-Bootstrap-Secret: $SECRETO" -H "Content-Type: application/json" \
  -d '{"tenantName":"...","tenantSlug":"...","adminEmail":"...","adminPassword":"...","adminFullName":"..."}'
```

Despues, borrar el secret `Bootstrap__Secret` y re-desplegar: el endpoint queda apagado.
