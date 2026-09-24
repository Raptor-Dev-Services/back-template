# HANDOFF - estado actual

Actualizado: 2026-09-24. Rama `main`, sin empujar (los commits son locales hasta que una persona decida).

## Que hay

El chasis generico de SaaS multi-tenant descrito en README y CHANGELOG, portado del chasis de un producto real
sin su logica de negocio. Build `-warnaserror` en 0/0 y 194 pruebas en verde:
Shared 26, Users 2, Authentication 22, Tenancy 20, Architecture 36, Integracion 88 (Postgres real por
Testcontainers).

## Como retomar

```bash
git submodule update --init          # Common fijado a aedf830
./scripts/dev-db.sh all              # base backtemplate en el devstack
cp .env.example .env                 # rellenar las tres claves
dotnet run --project src/Host/Host.Api   # http://localhost:5060
```

## Verificado en vivo (2026-09-24, devstack)

- `dev-db.sh reset` -> migracion InitialCreate + grants + RLS; `/health/ready` sano (postgres, object-storage).
- Bootstrap (secreto malo 401), login, login malo e inexistente identicos, refresh con rotacion, reuso -> familia
  revocada, `me` con permisos, listado paginado, 404 cruzado entre tenants, 401 con envelope, 429 del limite
  `auth` con envelope, subida real a MinIO y URL prefirmada que devuelve los mismos bytes, 404 de clave ajena,
  500 generico sin detalle (con el detalle en el log), sin contrasenas ni tokens en el log.
- Imagen en contenedor contra el devstack: `(healthy)` por la sonda .NET, URL firmada con `localhost:9000`.

## Pendiente

Ver BOARD.md (columna "Bloqueado / decision abierta" primero).
