# fix-todo - pendientes de la actualizacion de templates (2026-09-24)

Lo que quedo abierto tras portar el chasis de SocioFit a `back-template` y `front-template`.
Se marca `[x]` cada punto al quedar corregido **y verificado**, con el commit que lo cierra.

## SocioFit (antes de tocarlo: `git pull`)

- [x] **1. `sociofit-webclient/vite/csp.js`: hash CRLF.** En un checkout de Windows `index.html` queda en CRLF;
  el navegador normaliza a LF antes de hashear el script inline, asi que la CSP bloquea el script del tema.
  Normalizar antes de hashear + prueba que caiga sin el arreglo.
  - Hecho en `839b1b3` (webclient, sin push). Pull `--ff-only` previo de los tres repos: al dia.
  - Verificado: sin el arreglo la prueba cae (1 failed | 16 passed); con el, 593/593, lint 0 errores,
    build ok, y en el `dist` real el script sale en CRLF con su hash normalizado dentro de la CSP.
  - De paso: `node_modules` local estaba viejo (react-hooks 5.2 contra 7.1.1 del lockfile) y tumbaba el
    lint; se reinstalo con `npm ci`.

## devstack (catalogo)

- [x] **2. Filas de los templates en `devstack/PUERTOS.md`**: API 5060, web 5179, base `backtemplate`, roles
  `backtemplate_owner`/`backtemplate_app`, salud `/health/ready`, bucket `backtemplate`.
  - Hecho en `950bfb77` (catalogo, sin push). Filas en APIs, sondas, web clients y buckets.
- [x] **3. Roles, base y bucket en el init del devstack**, para que un volumen nuevo ya los traiga (hoy solo
  los crea `scripts/dev-db.sh`).
  - Hecho en `950bfb77`. Verificado en un Postgres desechable (55439) con el init montado: sin errores,
    8 bases, `backtemplate_owner` con BYPASSRLS y `backtemplate_app` sin el; `dev-db.sh all` del template
    corre encima (provision idempotente, InitialCreate, RLS en 3 tablas). El devstack vivo no se recreo:
    su volumen ya tenia la base por el script, asi que el init nuevo solo aplica a maquinas nuevas.

## back-template

Todo en `main` de back-template, sin push. Cada commit compila solo (`rebase --exec`, 0/0). Suite al final:
**194 pruebas en verde** (Shared 26, Users 2, Authentication 22, Tenancy 20, Architecture 36, Integracion 88).

> Aviso: mientras trabajaba, alguien subio Common a v2.1.3 (`f91234f`, 13:29) y **empujo** `main` con los 22
> commits anteriores. No fui yo. Mis commits van encima de ese y se probaron ya con Common v2.1.3.

- [x] **4. Proyecto `Tenancy.Tests`** con pruebas unitarias de los handlers de tareas y archivos (hoy solo
  integracion; `InternalsVisibleTo` apunta a un proyecto que no existe).
  - Hecho en `0196aca`: 20 pruebas (puerta del operador, sin bitacora al fallar, firma de bytes, borrado de
    compensacion, 404 de claves mal formadas o ajenas). Mutaciones: abrir la puerta tumba 3; quitar el borrado, 1.
- [x] **5. `UserRegisteredIntegrationEvent`**: se publica y nadie lo consume. Consumirlo o quitarlo.
  - Quitado en `b216288`. El ejemplo de evento con consumidor real sigue siendo `UserDisabledIntegrationEvent`.
- [x] **6. Purga de objetos huerfanos** en el bucket (objeto sin fila `StoredFile`): tarea programada que
  concilie.
  - Hecho en `be517d5`: `storage.purge-orphan-objects`, en seco por omision, gracia de 24 h, tenant por tenant
    con comprobacion de que la conexion lleva ese tenant (StoredFile esta bajo RLS: sin contexto veria 0 filas
    y lo borraria todo). 3 pruebas de integracion; 4 mutaciones, todas caen (sin contexto ni comprobacion
    borra lo legitimo y caen las 3). En vivo contra MinIO: en seco "se borrarian 2", encendida "Borrados 2".
- [x] **7. Acciones de GitHub fijadas por SHA** (hoy `@v4`, mutable).
  - Hecho en `819a47d`, con `dependabot.yml` para que los SHA no se queden viejos. YAML validado.
- [x] **8. Namespace de `UsersController`** (`Users.Presentation` en vez de `.Controllers`).
  - Hecho en `b216288`.
- [x] **9. Validacion 422 vs 400**: la regla `backend-architecture` del catalogo dice 400; la plantilla
  responde 422 (ADR-0006). Alinear la plantilla con el catalogo, que es la fuente de verdad.
  - Hecho en `ac3632b`. Confirmado antes: la tabla de `backend-architecture` dice 400, `api-response-envelope`
    remite a ella y Common responde 400. ADR-0007 reemplaza a ADR-0006. El front no dependia del 422.
- [x] **10. BOARD / HANDOFF / CHANGELOG** al dia con lo cerrado aqui.
  - Hecho en `cdab213`, junto con `docs/DEV-STACK.md`.

## front-template

Todo en `main` de front-template, sin push. Al final: lint 0/0, **219 pruebas en verde**, build ok.

- [x] **11. Pantalla de segundo factor**: paso de codigo TOTP en el login (`/auth/login/2fa`) y alta/baja de
  2FA en la cuenta, con pruebas.
  - Hecho en `a32cb65` (front) + `efd6478` (back: `/account/me` suma `twoFactorEnabled`, que la pantalla
    necesitaba para saber si ofrecer activar o apagar). 13 pruebas nuevas; 2 mutaciones, las dos caen.
  - Verificado contra el back real por el proxy de Vite: setup, activar (10 codigos), login con reto, codigo
    malo 401, TOTP y codigo de recuperacion validos, el mismo de recuperacion rechazado la 2a vez, apagar.
  - **Hallazgo de paso, grave y ya corregido** (back `dbe151f`): el log de Common guardaba en claro los
    codigos TOTP y los **diez codigos de recuperacion** al activarlos (tapa por nombre y `Code`/`Codes` no
    estaban en su lista). Renombrados a `OtpCode`/`OtpRecoveryCodes` (el JSON sigue diciendo `codes`) y una
    prueba de arquitectura revisa todos los requests y respuestas. En vivo, el log ahora dice `"***"`.
  - Sin QR: agregar `qrcode.react` es una dependencia nueva y el repo exige consultarlo. Queda en BOARD.
- [x] **12. Acciones de GitHub fijadas por SHA.**
  - Hecho en `cf36367`, con Dependabot.
- [x] **13. BOARD / CHANGELOG** al dia.
  - Hecho en `ccc589d` (tambien README, arquitectura y CLAUDE.md). De paso: el refresh y el logout mandan
    solo `refreshToken`, que era una deuda anotada por el agente del front.

## Limpieza local

- [x] **14. Imagenes Docker de verificacion** (`front-template:verify` y las que hayan quedado del back).
  - Borradas `front-template:verify`, `back-template-api:local` y `back-template-dev-api:latest`. Se conservan
    las imagenes base (Postgres la usa el devstack; ryuk, `minio/mc` y gitleaks son cache compartido).

## Fuera de alcance (no se hacen aqui; se dejan escritos)

- **Primer tag de los templates**: solo por orden explicita tuya (regla del repo). `TAGS.md` ya tiene la
  seccion "sin tag todavia".
- **Registro del release en `deploy.yml`**: el template no tiene el modulo de avisos de version; se agrega
  cuando un producto lo necesite (skill `release-announcements`).
- **Common: `InteractorPipeline` registra en FTL un 500 ordinario.** Es la libreria compartida de todos los
  productos; se cambia en su repo, no desde un template.
- **Common: el enmascarado del log es una lista negra por nombre.** Un secreto con un nombre fuera de la lista
  sale en claro (asi salieron los codigos 2FA). En la plantilla lo contiene una prueba; en Common protegeria a
  todos los productos. Anotado en el BOARD del back-template.
