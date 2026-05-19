# DB.md — Gestión de Base de Datos

Guía de referencia para agregar y modificar el esquema de PostgreSQL en este proyecto. Todo cambio de esquema pasa por el sistema de migraciones automáticas: **no se ejecuta SQL manual contra la base de datos**.

---

## Cadena de acceso a datos

```
ConnectionStrings:{T.Name}  (appsettings.json — clave = nombre de la clase marcadora)
    ↓
DbConnectionFactory<T>      → abre NpgsqlConnection para el marcador T
    ↓
DapperDbConnection<T>       → ejecuta Dapper + logs de performance automáticos
    ↓
{Entidad}Sql                → queries de una tabla — inyecta DapperDbConnection<MainDbConnection>
    ↓
{Entidad}Repository         → implementa interfaz de dominio
    ↓
Handler                     → lógica de negocio
```

`DbConnectionFactory<T>` y `DapperDbConnection<T>` son open generics registrados por `AddMainDatabase()`. Cualquier `...Sql` puede inyectar cualquier marcador sin registro adicional.

**Regla absoluta:** todo SQL vive en clases `...Sql`. Cero SQL inline en repositorios, handlers o servicios.

---

## Clases en `Shared/Database/`

| Clase | Lifetime | Responsabilidad |
|-------|----------|----------------|
| `MainDbConnection` | — | Clase marcadora vacía — su nombre es la clave de `ConnectionStrings` |
| `ReadonlyDbConnection` | — | Marcador de una segunda conexión (ejemplo) |
| `DbConnectionFactory<T>` | Singleton (open generic) | Abre `NpgsqlConnection` para el marcador T |
| `DapperDbConnection<T>` | Scoped (open generic) | Ejecuta Dapper + logs de performance automáticos |

Registro en `Shared.Database/ServiceCollectionEx.cs`:

```csharp
public static IServiceCollection AddMainDatabase(this IServiceCollection services)
{
    services.AddSingleton(typeof(DbConnectionFactory<>));
    services.AddScoped(typeof(DapperDbConnection<>));
    return services;
}
```

Una sola llamada registra la factory y la conexión para **todos** los marcadores. No se necesita registrar nada más cuando se agrega un marcador nuevo.

Llamado en `Host.Api/Program.cs`:

```csharp
builder.Services.AddMainDatabase();
```

---

## Métodos de `DapperDbConnection<T>`

| Método | Retorno | Uso |
|--------|---------|-----|
| `QueryAsync<T>` | `Task<IEnumerable<T>>` | Múltiples filas |
| `QuerySingleAsync<T>` | `Task<T?>` | 0 o 1 fila (lanza si hay más de 1) |
| `QueryFirstAsync<T>` | `Task<T?>` | Primera fila o null |
| `ExecuteAsync` | `Task<int>` | INSERT / UPDATE / DELETE → filas afectadas |
| `ExecuteScalarAsync<T>` | `Task<T>` | COUNT, EXISTS, RETURNING Id |

---

## Clases `...Sql`

Ubicación: `{Modulo}.Infrastructure/Persistence/SQLDB/{Entidad}Sql.cs`

Reglas:
- Recibe `DapperDbConnection<MainDbConnection>` por constructor.
- Agrupa **todos** los queries de esa tabla.
- SQL como raw strings `"""..."""` — nunca concatenación ni interpolación.
- Parámetros como objeto anónimo `new { param }`.
- Retorna entidades de dominio directamente — sin Row classes intermedias.

```csharp
using Shared.Database;
using Users.Domain.Entities;

namespace Users.Infrastructure.Persistence.SQLDB;

public sealed class UserProfilesSql
{
    private readonly DapperDbConnection<MainDbConnection> _db;
    public UserProfilesSql(DapperDbConnection<MainDbConnection> db) => _db = db;

    public Task<UserProfile?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
        _db.QuerySingleAsync<UserProfile>(
            """
            SELECT Id, PublicId, TenantId, BranchId, FullName, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.UserProfiles
            WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { publicId, tenantId },
            cancellationToken: ct);

    public Task<int> UpdateAsync(Guid publicId, long tenantId, string fullName, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.UserProfiles
            SET FullName = @fullName, UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { publicId, tenantId, fullName },
            cancellationToken: ct);
}
```

---

## Esquema de BD actual

| Tabla | Módulo dueño | Migración | Descripción |
|-------|-------------|-----------|-------------|
| `dbo.Tenants` | Tenancy | 001-002 | Empresas SaaS |
| `dbo.Branches` | Tenancy | 010-011 | Sucursales de un tenant |
| `dbo.Credentials` | Authentication | 020-021 | Login credentials (Email, PasswordHash, Role, TenantId, BranchId) |
| `dbo.UserProfiles` | Users | 030-031 | Datos de perfil (PublicId, FullName, TenantId, BranchId) |
| `dbo.RefreshTokens` | Authentication | 040-041 | Tokens JWT con FK a `dbo.Credentials(Id)` |

> **Separación importante:** `dbo.Credentials` es propiedad del módulo `Authentication`. `dbo.UserProfiles` es propiedad del módulo `Users`. La relación entre ellas se maneja por Integration Events, no por FK directa.

**Próxima entidad libre: bloque 050.**

---

## Archivos de migración actuales

`020_credentials.sql`:

```sql
CREATE TABLE IF NOT EXISTS dbo.Credentials
(
    Id           BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    PublicId     UUID         NOT NULL DEFAULT gen_random_uuid(),
    TenantId     BIGINT       NOT NULL REFERENCES dbo.Tenants(Id),
    BranchId     BIGINT       NOT NULL REFERENCES dbo.Branches(Id),
    Email        VARCHAR(256) NOT NULL,
    PasswordHash VARCHAR(72)  NOT NULL,
    Role         VARCHAR(50)  NOT NULL DEFAULT 'User',
    IsActive     BOOLEAN      NOT NULL DEFAULT TRUE,
    CreatedAtUtc TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now())),
    UpdatedAtUtc TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))
);
```

`030_user_profiles.sql`:

```sql
CREATE TABLE IF NOT EXISTS dbo.UserProfiles
(
    Id           BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    PublicId     UUID         NOT NULL DEFAULT gen_random_uuid(),
    TenantId     BIGINT       NOT NULL REFERENCES dbo.Tenants(Id),
    BranchId     BIGINT       NOT NULL REFERENCES dbo.Branches(Id),
    FullName     VARCHAR(200) NOT NULL,
    IsActive     BOOLEAN      NOT NULL DEFAULT TRUE,
    CreatedAtUtc TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now())),
    UpdatedAtUtc TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))
);
```

`040_refresh_tokens.sql`:

```sql
CREATE TABLE IF NOT EXISTS dbo.RefreshTokens
(
    Id           BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    CredentialId BIGINT       NOT NULL REFERENCES dbo.Credentials(Id),
    Token        VARCHAR(256) NOT NULL,
    ExpiresAtUtc TIMESTAMP(0) NOT NULL,
    IsRevoked    BOOLEAN      NOT NULL DEFAULT FALSE,
    CreatedAtUtc TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now())),
    CONSTRAINT UQ_RefreshTokens_Token UNIQUE (Token)
);
```

---

## Migraciones SQL

Ubicación: `Host.Api/Services/Schema Migration/Tables/`

**Reglas:**
- Todos los archivos son idempotentes: `CREATE TABLE IF NOT EXISTS`.
- Nunca editar migraciones ya aplicadas → nueva migración con número mayor.
- Esquema `dbo` para todas las tablas.
- Fechas UTC: `TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))`.
- Se ejecutan automáticamente al iniciar la aplicación (`AddSchemaMigrations()`).
- Numeración: 3 dígitos, bloques de 10 por entidad.

### Plantilla de nueva tabla

`NNN_nueva_tabla.sql`:

```sql
CREATE TABLE IF NOT EXISTS dbo.NuevaTabla
(
    Id           BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    PublicId     UUID         NOT NULL DEFAULT gen_random_uuid(),
    TenantId     BIGINT       NOT NULL REFERENCES dbo.Tenants(Id),
    -- columnas de negocio
    Nombre       VARCHAR(200) NOT NULL,
    IsActive     BOOLEAN      NOT NULL DEFAULT TRUE,
    CreatedAtUtc TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now())),
    UpdatedAtUtc TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))
);
```

`NNN+1_nueva_tabla_indexes.sql`:

```sql
CREATE UNIQUE INDEX IF NOT EXISTS UX_NuevaTabla_PublicId ON dbo.NuevaTabla (PublicId);
CREATE INDEX        IF NOT EXISTS IX_NuevaTabla_TenantId ON dbo.NuevaTabla (TenantId);
CREATE INDEX        IF NOT EXISTS IX_NuevaTabla_IsActive ON dbo.NuevaTabla (IsActive);
```

---

## Transacciones

`MainDapperDbConnection` **no soporta** `IDbTransaction`. Para operaciones atómicas entre múltiples tablas, inyectar `IOpenDbConnectionFactory` directamente en el repositorio y usar Dapper sobre la conexión:

```csharp
public sealed class MiRepository : IMiRepository
{
    private readonly IOpenDbConnectionFactory _factory;

    public async Task OperacionAtomicaAsync(CancellationToken ct = default)
    {
        using var connection = await _factory.GetOpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO ...", new { ... }, transaction, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO ...", new { ... }, transaction, cancellationToken: ct));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
```

---

## Connection String

`appsettings.json`:

```json
"ConnectionStrings": {
  "MainDbConnection": "Host=localhost;Port=5432;Database=back_template;Username=postgres;Password=postgres"
}
```

`appsettings.Local.json` (dev diario):

```json
"ConnectionStrings": {
  "MainDbConnection": "Host=localhost;Port=5432;Database=back_template_local;Username=postgres;Password=postgres"
}
```

---

## Logging de performance

| Tiempo | Nivel de log |
|--------|-------------|
| < 300 ms | `Debug` |
| ≥ 300 ms | `Warning` |
| ≥ 1 000 ms | `Error` |
| ≥ 2 000 ms | `Critical` |

Para incluir el texto SQL en los logs (solo `Local`):

```json
"CustomLogging": { "IncludeSqlText": true }
```

---

## Patrones SQL avanzados

### Paginación con COUNT total en un solo round-trip

```sql
SELECT Id, PublicId, Nombre, COUNT(*) OVER() AS TotalCount
FROM dbo.MiTabla
WHERE TenantId = @tenantId AND IsActive = TRUE
ORDER BY CreatedAtUtc DESC
LIMIT @pageSize OFFSET @offset;
```

### INSERT con RETURNING

```sql
INSERT INTO dbo.MiTabla (Nombre, TenantId)
VALUES (@nombre, @tenantId)
RETURNING Id, PublicId, Nombre, TenantId, IsActive, CreatedAtUtc, UpdatedAtUtc;
```

Usar `QueryFirstAsync<T>` con `!` null-forgiving en la clase Sql.

### Diferencias PostgreSQL vs SQL Server

| Patrón | SQL Server | PostgreSQL |
|--------|-----------|------------|
| Identidad | `IDENTITY(1,1)` | `GENERATED BY DEFAULT AS IDENTITY` |
| Valor al insertar | `OUTPUT inserted.*` | `RETURNING col1, col2, ...` |
| Fecha UTC | `GETUTCDATE()` | `timezone('utc', now())` |
| Paginación | `OFFSET N FETCH NEXT M` | `LIMIT M OFFSET N` |
| UUID default | — | `DEFAULT gen_random_uuid()` |
| Existe | `IF EXISTS (...)` | `SELECT EXISTS (...)` |
| Cast | `CAST(x AS INT)` | `x::int` |

---

## DI — Lifetimes

| Clase | Lifetime |
|-------|---------|
| `DbConnectionFactory<T>` (open generic) | Singleton |
| `DapperDbConnection<T>` (open generic) | Scoped |
| `{Entidad}Sql` | Scoped |
| `{Entidad}Repository` | Scoped |
