# ADR-0005 - Almacenamiento de objetos con propiedad por tenant

- Estado: Aceptado
- Fecha: 2026-09-24
- Deciden: mantenedores de back-template

## Contexto y problema

Un endpoint que firma URLs de lectura para cualquier clave a cualquier token valido deja leer archivos ajenos.
Un Content-Type declarado por el cliente se puede falsear.

## Decision

Bucket privado y URLs prefirmadas de vida corta. Cada subida registra su dueno en `StoredFile` (TenantEntity, con
RLS); firmar exige que la clave sea del tenant de la peticion y responde 404 si no, sin distinguir "ajena" de
"inexistente". La subida contrasta la firma de bytes con el tipo declarado, genera la clave
`{tenant}/{yyyy}/{MM}/{guid}{ext}` con la extension del tipo validado y guarda el nombre original solo saneado.
Dos endpoints de conexion: `Endpoint` para hablar con el servidor y `PublicEndpoint` para firmar.

## Cumplimiento

`FilesTests`, `StoragePrimitivesTests`, check `object-storage` en `/health/ready`.
