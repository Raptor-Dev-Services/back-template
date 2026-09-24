# Base de datos

PostgreSQL 17 con EF Core 10 (Npgsql). Un **solo** `AppDbContext` (`Shared.Infrastructure/Persistence/AppDbContext.cs`)
para todo el sistema; cada modulo le aporta sus tablas.

## Dos roles

| Rol | Para que | Privilegios |
|---|---|---|
| `backtemplate_owner` | migraciones y scripts de RLS | dueno del esquema (DDL) |
| `backtemplate_app` | la API en ejecucion | `SELECT, INSERT, UPDATE` (sin `DELETE`), `NOBYPASSRLS` |

La API conecta **siempre** con el rol de la aplicacion (`ConnectionStrings__DefaultConnection`). `RlsRoleGuard`
tumba el arranque si ese rol es superusuario o tiene `BYPASSRLS`: con uno asi las policies se ignoran sin ningun
sintoma. Sin `DELETE`, un borrado fisico falla en el motor aunque alguien lo escriba (`000_app_role_grants.sql`).

## Convenciones que aplica el DbContext

- **Esquema `public`**, tablas en singular y PascalCase (`UserProfile`, `AuditLog`).
- **Entidades base** (`Shared.Kernel/Domain/TenantEntity.cs`): `TenantEntity` (lleva `TenantId`) y `GlobalEntity`.
  Ambas traen `Id` (bigint identity), auditoria (`CreatedAtUtc`, `UpdatedAtUtc`, y en las de tenant el actor
  `CreatedByUserId`/`UpdatedByUserId`/`DeletedByUserId`), soft delete (`IsDeleted`, `DeletedAtUtc`) y `Version`.
- **Filtros globales con nombre** (`QueryFilterNames`): `tenant` (`TenantId == tenant actual`) y `soft_delete`.
  Se ignoran por nombre, nunca todos a la vez sin querer: `IgnoreQueryFilters([QueryFilterNames.Tenant])`.
- **Soft delete**: un `Remove` se convierte en marca; nunca hay `DELETE`.
- **Auditoria** automatica en `SaveChanges`: fechas en UTC y actor desde `ICurrentUser`. El `TenantId` de una fila
  nueva se sella desde el contexto y es inmutable despues.
- **UTC**: toda fecha es `timestamp with time zone`; Npgsql exige `Kind=Utc`.
- **Concurrencia optimista**: `Version` se mapea a la columna de sistema `xmin`; perder la carrera es 409.
- **Unicidad**: `23505` se traduce a 409 legible. Los indices unicos de tablas con soft delete se filtran con
  `IsDeleted = false`.

## Migraciones

- Viven en `src/Shared/Shared.Infrastructure/Persistence/Migrations/` (hoy una sola: `InitialCreate`).
- `dotnet ef` usa `Host.Api/Persistence/AppDbContextFactory.cs`, que enumera los modulos con tablas
  (`Modules`) y prefiere `ConnectionStrings__Migrations` (rol dueno). La version de `dotnet-ef` la fija
  `dotnet-tools.json`.
- **Nunca corren al arrancar la API.** En desarrollo: `scripts/dev-db.sh`. En produccion: el pipeline aplica el
  script idempotente que viaja en la imagen (ver [Deployment.md](Deployment.md)).

```sh
# nueva migracion
dotnet tool restore
dotnet ef migrations add NombreDelCambio -p src/Shared/Shared.Infrastructure -s src/Host/Host.Api -o Persistence/Migrations
```

Escribirlas **expand/contract**: una columna nueva nace nullable o con default, y se borra lo viejo en un release
posterior. El rollback del pipeline no revierte el esquema.

## Row-Level Security

Segunda barrera de aislamiento, en el motor (`Persistence/Sql/001_enable_rls.sql`, idempotente). Detalle en
[MultiTenancy.md](MultiTenancy.md). Toda tabla nueva con `TenantId` necesita su bloque ahi o una exclusion
justificada; si no, falla `RlsIsolationTests` y el guardia del deploy.

## Desarrollo local

```sh
./scripts/dev-db.sh all      # provision (roles + base + grants) -> migrate -> rls
./scripts/dev-db.sh status   # roles, migraciones aplicadas, tablas con RLS
./scripts/dev-db.sh reset    # BORRA solo la base backtemplate y corre all
```

Corre `psql` dentro del contenedor del devstack (`PG_CONTAINER`, por omision `devstack-postgres`). Ver
`docs/DEV-STACK.md`.

## Tablas actuales

| Tabla | Modulo | Tenant | RLS |
|---|---|---|---|
| `Tenant` | Tenancy | global | - |
| `UserProfile` | Users | si | si |
| `UserCredential`, `RefreshToken`, `PasswordSetupToken`, `TwoFactorRecoveryCode` | Authentication | si | excluidas (se leen antes de conocer el tenant) |
| `Permission` | Authentication | global | - |
| `Role`, `RolePermission`, `UserRole` | Authentication | si | excluidas (RbacRepository re-acota el tenant a mano) |
| `AuditLog` | Shared.Infrastructure | si | si |
| `StoredFile` | Shared.Infrastructure | si | si |
| `AutomatedTaskDefinition`, `AutomatedTaskRun` | Shared.Infrastructure | global | - |

Acceso a datos solo desde repositorios en `Infrastructure`. El unico SQL crudo es el reclamo atomico de tareas
(`AutomatedTaskRepository`, `FromSqlInterpolated`, parametrizado); nunca se concatena SQL.
