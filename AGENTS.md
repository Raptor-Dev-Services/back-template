# AGENTS.md

Instrucciones para cualquier agente de codigo que trabaje en este repo. Son las mismas que para Claude Code:
**lee [CLAUDE.md](CLAUDE.md)**, que es la fuente unica. Este archivo existe para las herramientas que buscan
`AGENTS.md` por convencion y no se mantiene por separado.

Resumen de lo que no se negocia:

- Patron de caso de uso de Common (Request / Handler / Responses / Presenter / Controller), registro manual.
- `Application` nunca referencia `Infrastructure`, ni al reves; modulos solo se ven por `Contracts`.
- El tenant sale solo del claim `tenant_id`; toda tabla con `TenantId` lleva RLS o una exclusion justificada.
- `Common/` no se edita. Secretos solo por entorno.
- Build `-warnaserror` y todas las pruebas en verde antes de cada commit.
