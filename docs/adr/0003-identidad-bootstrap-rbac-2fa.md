# ADR-0003 - Identidad: bootstrap, RBAC por permisos y segundo factor

- Estado: Aceptado
- Fecha: 2026-09-24
- Deciden: mantenedores de back-template

## Contexto y problema

La plantilla original tenia `/register` anonimo (cualquiera creaba un tenant y se hacia Admin), roles
comparados como texto y refresh tokens guardados en claro.

## Decision

- El primer tenant y su administrador se crean con `POST /api/v1/bootstrap/tenant` y un secreto compartido de
  32+ bytes (comparacion en tiempo constante); sin secreto el endpoint esta apagado. El resto de usuarios entra
  por invitacion (enlace de un solo uso por correo).
- RBAC por PERMISOS: catalogo unico en `KnownPermissions` + `RbacCatalog`, re-sembrado al arrancar; el login emite
  un claim `permission` por codigo y cada endpoint exige `perm:<codigo>`. Un Admin no puede conceder mas de lo que tiene.
- Access token de 15 min; refresh de 14 dias guardado como hash SHA-256, rotado en cada uso, y el reuso de uno
  viejo revoca toda la familia.
- TOTP opcional por usuario: secreto cifrado con AES-GCM (clave distinta del JWT), anti-replay por paso,
  codigos de recuperacion hasheados de un solo uso; desactivarlo exige un segundo factor.

## Consecuencias

- Contrato con el front: permisos `users.read`, `users.manage`, `audit.read`, `tasks.manage`, `files.read`,
  `files.write`; body `refreshToken` en refresh y logout.
- Cambiar `Totp__EncryptionKey` deja ilegibles los secretos enrolados.

## Cumplimiento

`AuthorizationSurfaceTests` (todo endpoint con permiso o justificado), `SessionTests`, `ProvisioningTests`,
`TwoFactorTests` y las pruebas unitarias de Authentication.
