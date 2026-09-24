# HANDOFF - estado actual

Actualizado: 2026-09-24. Rama `main`, **empujada** (el usuario lo autorizo al cierre), arbol limpio, sin otras ramas.

## Que hay

El chasis generico de SaaS multi-tenant descrito en README y CHANGELOG, portado del chasis de un producto real
sin su logica de negocio. Build `-warnaserror` en 0/0 y 195 pruebas en verde:
Shared 26, Users 2, Authentication 22, Tenancy 20, Architecture 37, Integracion 88 (Postgres real por
Testcontainers). Remedido el 2026-09-24 sobre `23d0d55`; el CI de `main` tambien en verde.

- `Common` fijado a **v2.1.3** (`dace97a`), sin cambios de codigo aqui.
- Dependabot quedo activo: su primer PR (acciones de GitHub) se mergeo en `23d0d55`. Subio de version
  **mayor** las cuatro acciones de Docker (`build-push` 7, `login` 4, `metadata` 6, `buildx` 4), que
  solo usan los workflows de despliegue: **el primer despliegue es su prueba**. Si falla, mira ahi.

## Como retomar

```bash
git submodule update --init          # Common fijado a v2.1.3 (dace97a)
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
