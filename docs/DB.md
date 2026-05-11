# DB.md — Gestión de Base de Datos

Guía de referencia para agregar y modificar el esquema de PostgreSQL en este proyecto.  
Todo cambio de esquema pasa por el sistema de migraciones automáticas: **no se ejecuta SQL manual contra la base de datos**.

---

## Estructura de carpetas en Infrastructure

```
Infrastructure/
├── PostgreSql/                              ← conexión a la BD
│   ├── MainDbConnection.cs                  ← tipo marcador (clave de ConnectionStrings)
│   ├── MainDbConnectionFactory.cs           ← abre NpgsqlConnections
│   └── MainDapperDbConnection.cs            ← ÚNICO punto de ejecución SQL
│
└── Persistence/
    └── SQLDB/
        └── Main/
            └── {Modulo}/
                └── {Entidad}Sql.cs          ← queries de una tabla
```

> **No existe** una carpeta `Dapper/`, `DbModels/`, `Connections/` ni `System/` en este proyecto.  
> Las clases `...Sql` mapean directamente a entidades de dominio, sin Row classes intermedias.

---

## 1. Tipos marcadores y fábricas

### `Infrastructure/PostgreSql/MainDbConnection.cs`

Clase marcadora vacía. Su **nombre** es exactamente la clave de `ConnectionStrings` en `appsettings.json`.

```csharp
namespace Infrastructure.PostgreSql;

public sealed class MainDbConnection;
```

### `Infrastructure/PostgreSql/MainDbConnectionFactory.cs`

Extiende la fábrica genérica de `Common.PostgreSql`. Lee `ConnectionStrings:MainDbConnection` de `IConfiguration` y crea `NpgsqlConnection`s abiertas.

```csharp
using Common.PostgreSql;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.PostgreSql;

public sealed class MainDbConnectionFactory : ConfigurationNpgsqlConnectionFactory<MainDbConnection>
{
    public MainDbConnectionFactory(IConfiguration configuration) : base(configuration) { }
}
```

### `Infrastructure/PostgreSql/MainDapperDbConnection.cs`

Único punto de ejecución SQL del proyecto. Extiende `DapperSqlDbConnectionBase` (de `Common`), que incluye logging de performance por query (Stopwatch + SHA256 + Serilog).

```csharp
using Common.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.PostgreSql;

public sealed class MainDapperDbConnection : DapperSqlDbConnectionBase
{
    public MainDapperDbConnection(
        MainDbConnectionFactory factory,
        ILogger<MainDapperDbConnection> logger,
        IConfiguration configuration)
        : base(factory, logger, configuration.GetValue<bool>("CustomLogging:IncludeSqlText"))
    { }
}
```

**Cadena completa:**

```
ConnectionStrings:MainDbConnection (appsettings.json)
    ↓
MainDbConnectionFactory  (abre NpgsqlConnection)
    ↓
MainDapperDbConnection   (ejecuta Dapper + logs de performance)
    ↓
{Entidad}Sql classes     (inyectan MainDapperDbConnection)
```

---

## 2. Métodos disponibles en `MainDapperDbConnection`

Todos los métodos tienen `queryName?`, `level?` y `cancellationToken` opcionales. Usa siempre `cancellationToken: ct` como parámetro nombrado.

| Método | Retorno | Uso |
|--------|---------|-----|
| `QueryAsync<T>` | `Task<IEnumerable<T>>` | Múltiples filas |
| `QuerySingleAsync<T>` | `Task<T?>` | 0 o 1 fila (lanza si hay más de 1) |
| `QueryFirstAsync<T>` | `Task<T?>` | Primera fila o null |
| `ExecuteAsync` | `Task<int>` | INSERT / UPDATE / DELETE → filas afectadas |
| `ExecuteScalarAsync<T>` | `Task<T>` | COUNT, EXISTS, o cualquier escalar |

> `QuerySingleAsync` y `QueryFirstAsync` retornan **nullable** (`T?`).  
> Para `InsertAsync` que siempre retorna fila, usa `QueryFirstAsync` con `!` null-forgiving.

**Logging de performance automático** (umbral en `DapperSqlDbConnectionBase`):

| Tiempo | Nivel de log |
|--------|-------------|
| < 300 ms | `Debug` (o el nivel que pases) |
| ≥ 300 ms | `Warning` |
| ≥ 1 000 ms | `Error` |
| ≥ 2 000 ms | `Critical` |

Para incluir el texto SQL en los logs, agrega en `appsettings.json`:

```json
"CustomLogging": {
  "IncludeSqlText": true
}
```

---

## 3. Clases `...Sql`

Cada tabla tiene una clase `{Entidad}Sql` bajo `Infrastructure/Persistence/SQLDB/Main/{Modulo}/`.

**Reglas:**
- Recibe `MainDapperDbConnection` por constructor.
- Agrupa **todos** los queries de esa tabla — ninguno fuera de ella.
- **No acepta `IDbTransaction?`** — esta implementación no soporta transacciones vía `...Sql`.
- SQL como raw strings `"""..."""`. Nunca concatenación ni interpolación.
- Parámetros siempre como objeto anónimo `new { param }`.
- Retorna entidades de dominio directamente.

### Ejemplo completo — `ExampleUsersSql.cs`

```csharp
using Domain.Entities.Example;
using Infrastructure.PostgreSql;

namespace Infrastructure.Persistence.SQLDB.Main.Example;

public sealed class ExampleUsersSql
{
    private readonly MainDapperDbConnection _db;

    public ExampleUsersSql(MainDapperDbConnection db) => _db = db;

    public Task<ExampleUser?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        _db.QuerySingleAsync<ExampleUser>(
            """
            SELECT Id, PublicId, FullName, Email, Department, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.ExampleUsers
            WHERE PublicId = @publicId;
            """,
            new { publicId },
            cancellationToken: ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM dbo.ExampleUsers
                WHERE LOWER(Email) = LOWER(@email)
            );
            """,
            new { email },
            cancellationToken: ct);

    public Task<IEnumerable<ExampleUser>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default) =>
        _db.QueryAsync<ExampleUser>(
            """
            SELECT Id, PublicId, FullName, Email, Department, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.ExampleUsers
            ORDER BY CreatedAtUtc DESC
            LIMIT @pageSize OFFSET @offset;
            """,
            new { offset = (page - 1) * pageSize, pageSize },
            cancellationToken: ct);

    public Task<int> GetCountAsync(CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM dbo.ExampleUsers;
            """,
            cancellationToken: ct);

    public async Task<ExampleUser> InsertAsync(
        string fullName, string email, string department, string notes,
        CancellationToken ct = default) =>
        (await _db.QueryFirstAsync<ExampleUser>(
            """
            INSERT INTO dbo.ExampleUsers (FullName, Email, Department, Notes)
            VALUES (@fullName, @email, @department, @notes)
            RETURNING Id, PublicId, FullName, Email, Department, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc;
            """,
            new { fullName, email, department, notes },
            cancellationToken: ct).ConfigureAwait(false))!;

    public Task<int> UpdateAsync(
        Guid publicId, string fullName, string department, string notes,
        CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.ExampleUsers
            SET FullName     = @fullName,
                Department   = @department,
                Notes        = @notes,
                UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId
              AND IsActive  = TRUE;
            """,
            new { publicId, fullName, department, notes },
            cancellationToken: ct);

    public Task<int> DisableAsync(Guid publicId, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.ExampleUsers
            SET IsActive     = FALSE,
                UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId = @publicId
              AND IsActive  = TRUE;
            """,
            new { publicId },
            cancellationToken: ct);
}
```

---

## 4. Transacciones

`DapperSqlDbConnectionBase` **no soporta `IDbTransaction`**. Si necesitas una operación atómica que afecte múltiples tablas, abre la transacción directamente en el repositorio usando `IOpenDbConnectionFactory` e invoca Dapper sobre esa conexión manualmente:

```csharp
public sealed class MiRepository : IMiRepository
{
    private readonly IOpenDbConnectionFactory _factory;
    private readonly EntidadASql _entidadA;
    private readonly EntidadBSql _entidadB;

    // ...

    public async Task OperacionAtomicaAsync(CancellationToken ct = default)
    {
        using var connection = await _factory.GetOpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition("INSERT INTO ...", new { ... }, transaction, cancellationToken: ct));
            await connection.ExecuteAsync(
                new CommandDefinition("INSERT INTO ...", new { ... }, transaction, cancellationToken: ct));
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

> Este patrón usa Dapper directamente sobre la `IDbConnection`. Solo úsalo cuando la atomicidad sea estrictamente necesaria.

---

## 5. Registro de dependencias (DI)

`Infrastructure/ServiceCollectionEx.cs`:

```csharp
using Common.Data;
using Domain.Repositories.Example;
using Infrastructure.Persistence.SQLDB.Main.Example;
using Infrastructure.PostgreSql;
using Infrastructure.Repositories.Example;

public static IServiceCollection AddInfrastructureServices(
    this IServiceCollection services, IConfiguration configuration)
{
    // Fábricas (Singleton)
    services.AddSingleton<MainDbConnectionFactory>();
    services.AddSingleton<IOpenDbConnectionFactory>(
        sp => sp.GetRequiredService<MainDbConnectionFactory>());

    // Wrapper SQL (Scoped — una instancia por request)
    services.AddScoped<MainDapperDbConnection>();

    // Clases Sql (Scoped)
    services.AddScoped<ExampleUsersSql>();
    services.AddScoped<MiEntidadSql>();   // ← agregar aquí al crear entidades

    // Repositorios (Scoped)
    services.AddScoped<IExampleUserRepository, ExampleUserRepository>();
    services.AddScoped<IMiEntidadRepository, MiEntidadRepository>(); // ← agregar aquí

    return services;
}
```

**Lifetimes:**
- `MainDbConnectionFactory` → `Singleton` (sin estado mutable)
- `IOpenDbConnectionFactory` → alias Singleton del factory
- `MainDapperDbConnection`, `...Sql`, repositorios → `Scoped`

---

## 6. Migraciones de esquema (PostgreSQL)

Las migraciones viven en:

```
Host/Services/Schema Migration/Tables/
```

El host las ejecuta automáticamente en orden numérico al iniciar, vía `SchemaMigrationHostedService` (de `Common`). **No se ejecuta SQL manual** contra la base de datos.

Para que los archivos `.sql` se copien al directorio de salida, el `Host.csproj` incluye:

```xml
<ItemGroup>
  <Content Include="Services\Schema Migration\Tables\*.sql">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Y en `Host/Program.cs`:

```csharp
builder.Services.AddSchemaMigrations();
```

### Convención de numeración

Archivos de 3 dígitos en orden secuencial. Cada entidad ocupa un bloque de 10 números, lo que deja espacio para vistas, triggers o ALTER TABLE del mismo módulo:

| N | Tipo | Ejemplo |
|---|------|---------|
| `X01` | `CREATE TABLE` | `001_example_users.sql` |
| `X02` | Índices | `002_example_users_indexes.sql` |
| `X03` | Vistas | `003_example_users_view.sql` |
| `X04` | Triggers / validaciones | `004_example_users_trigger.sql` |
| libre | `ALTER TABLE` incremental | `015_example_users_add_phone.sql` |

La primera entidad real del proyecto empieza en bloque `010` (`010_mi_entidad.sql`, `011_mi_entidad_indexes.sql`), dejando `001–009` para el módulo Example de la plantilla.

También se pueden combinar tabla e índices en **un único archivo** si la entidad es sencilla:

```
010_mi_entidad.sql   ← CREATE TABLE + CREATE INDEX en el mismo archivo
```

### Reglas absolutas

- Todos los archivos son **idempotentes**: `CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`, etc.
- **Nunca editar una migración ya aplicada.** Si necesitas cambiar algo → nueva migración con número mayor.
- Esquema `dbo` para todas las tablas.
- Fechas en UTC: `TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))`.

---

## Plantillas SQL

### Nueva tabla — `NNN_nombre_tabla.sql`

```sql
CREATE TABLE IF NOT EXISTS dbo.NombreTabla
(
    Id           BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    PublicId     UUID          NOT NULL DEFAULT gen_random_uuid(),

    -- columnas de negocio
    Nombre       VARCHAR(200)  NOT NULL,
    Descripcion  TEXT          NOT NULL DEFAULT '',
    Monto        NUMERIC(12,2) NOT NULL DEFAULT 0,
    Estado       VARCHAR(30)   NOT NULL DEFAULT 'Activo',
    EstaActivo   BOOLEAN       NOT NULL DEFAULT TRUE,

    -- FK obligatoria
    OtraTablaId  BIGINT        NOT NULL REFERENCES dbo.OtraTabla (Id),
    -- FK opcional
    OpcionalId   BIGINT                 REFERENCES dbo.Opcional (Id),

    CreatedAtUtc TIMESTAMP(0)  NOT NULL DEFAULT (timezone('utc', now())),
    UpdatedAtUtc TIMESTAMP(0)  NOT NULL DEFAULT (timezone('utc', now())),

    CONSTRAINT UQ_NombreTabla_Nombre  UNIQUE (Nombre),
    CONSTRAINT CK_NombreTabla_Monto   CHECK (Monto >= 0),
    CONSTRAINT CK_NombreTabla_Estado  CHECK (Estado IN ('Activo', 'Inactivo', 'Pendiente'))
);
```

Columnas estándar en toda entidad:

| Columna | Tipo | Propósito |
|---------|------|-----------|
| `Id` | `BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY` | PK interna, nunca expuesta en la API |
| `PublicId` | `UUID NOT NULL DEFAULT gen_random_uuid()` | Identificador externo de la API |
| `CreatedAtUtc` | `TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))` | Fecha de creación UTC |
| `UpdatedAtUtc` | `TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))` | Última actualización UTC |

### Índices — `NNN+1_nombre_tabla_indexes.sql`

```sql
-- Siempre presente: único sobre PublicId
CREATE UNIQUE INDEX IF NOT EXISTS UX_NombreTabla_PublicId
    ON dbo.NombreTabla (PublicId);

-- FK no nullable
CREATE INDEX IF NOT EXISTS IX_NombreTabla_OtraTablaId
    ON dbo.NombreTabla (OtraTablaId);

-- FK nullable — índice parcial solo en filas con valor
CREATE INDEX IF NOT EXISTS IX_NombreTabla_OpcionalId
    ON dbo.NombreTabla (OpcionalId)
    WHERE OpcionalId IS NOT NULL;

-- Búsqueda case-insensitive
CREATE INDEX IF NOT EXISTS IX_NombreTabla_Nombre
    ON dbo.NombreTabla (LOWER(Nombre));

-- Queries de "últimos N"
CREATE INDEX IF NOT EXISTS IX_NombreTabla_CreatedAt
    ON dbo.NombreTabla (CreatedAtUtc DESC);

-- Filtro frecuente
CREATE INDEX IF NOT EXISTS IX_NombreTabla_Estado
    ON dbo.NombreTabla (Estado);
```

Convención de nombres:

| Prefijo | Tipo |
|---------|------|
| `UX_` | `UNIQUE INDEX` |
| `IX_` | Índice no único |
| `UQ_` | Unique constraint declarado en `CREATE TABLE` |
| `CK_` | Check constraint declarado en `CREATE TABLE` |

### Vista — `NNN+2_nombre_view.sql`

```sql
CREATE OR REPLACE VIEW dbo.v_ResumenVentas AS
SELECT
    s.PublicId,
    s.Total,
    s.Status,
    s.CreatedAtUtc,
    u.FirstName || ' ' || u.LastName AS CashierName
FROM dbo.Sales s
JOIN dbo.Users u ON u.Id = s.CashierId;
```

### ALTER TABLE — nueva columna

```sql
ALTER TABLE dbo.NombreTabla
    ADD COLUMN IF NOT EXISTS NuevaColumna VARCHAR(500);
```

Múltiples columnas:

```sql
ALTER TABLE dbo.Branches
    ADD COLUMN IF NOT EXISTS DisplayName VARCHAR(100),
    ADD COLUMN IF NOT EXISTS ThemeColor  VARCHAR(7),
    ADD COLUMN IF NOT EXISTS LogoUrl     TEXT;
```

### ALTER TABLE — modificar constraint

```sql
ALTER TABLE dbo.StockLevels
    DROP CONSTRAINT IF EXISTS UQ_StockLevels_ProductId;

ALTER TABLE dbo.StockLevels
    ADD COLUMN IF NOT EXISTS VariantId BIGINT REFERENCES dbo.ProductVariants (Id);

CREATE UNIQUE INDEX IF NOT EXISTS UQ_StockLevels_Product_Only
    ON dbo.StockLevels (ProductId)
    WHERE VariantId IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS UQ_StockLevels_Variant
    ON dbo.StockLevels (VariantId)
    WHERE VariantId IS NOT NULL;
```

---

## 7. Flujo completo para agregar una entidad

### Paso 1 — Migraciones SQL

```
Host/Services/Schema Migration/Tables/
    NNN_mi_entidad.sql
    NNN+1_mi_entidad_indexes.sql
```

### Paso 2 — Entidad de dominio

`Domain/Entities/{Modulo}/MiEntidad.cs`

```csharp
namespace Domain.Entities.MiModulo;

public sealed class MiEntidad
{
    public long     Id           { get; init; }
    public Guid     PublicId     { get; init; }
    public string   Nombre       { get; init; } = string.Empty;
    public bool     EstaActivo   { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
```

### Paso 3 — Clase Sql

`Infrastructure/Persistence/SQLDB/Main/{Modulo}/MiEntidadSql.cs`

```csharp
using Domain.Entities.MiModulo;
using Infrastructure.PostgreSql;

namespace Infrastructure.Persistence.SQLDB.Main.MiModulo;

public sealed class MiEntidadSql
{
    private readonly MainDapperDbConnection _db;

    public MiEntidadSql(MainDapperDbConnection db) => _db = db;

    public Task<MiEntidad?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        _db.QuerySingleAsync<MiEntidad>(
            """
            SELECT Id, PublicId, Nombre, EstaActivo, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.MiEntidad
            WHERE PublicId = @publicId;
            """,
            new { publicId },
            cancellationToken: ct);

    public async Task<MiEntidad> InsertAsync(string nombre, CancellationToken ct = default) =>
        (await _db.QueryFirstAsync<MiEntidad>(
            """
            INSERT INTO dbo.MiEntidad (Nombre)
            VALUES (@nombre)
            RETURNING Id, PublicId, Nombre, EstaActivo, CreatedAtUtc, UpdatedAtUtc;
            """,
            new { nombre },
            cancellationToken: ct).ConfigureAwait(false))!;

    public Task<int> UpdateAsync(Guid publicId, string nombre, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.MiEntidad
            SET Nombre       = @nombre,
                UpdatedAtUtc = timezone('utc', now())
            WHERE PublicId   = @publicId;
            """,
            new { publicId, nombre },
            cancellationToken: ct);
}
```

### Paso 4 — Interfaz de repositorio

`Domain/Repositories/{Modulo}/IMiEntidadRepository.cs`

```csharp
using Domain.Entities.MiModulo;

namespace Domain.Repositories.MiModulo;

public interface IMiEntidadRepository
{
    Task<MiEntidad?> GetByPublicIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MiEntidad> InsertAsync(string nombre, CancellationToken cancellationToken = default);
}
```

### Paso 5 — Implementación del repositorio

`Infrastructure/Repositories/{Modulo}/MiEntidadRepository.cs`

```csharp
using Domain.Entities.MiModulo;
using Domain.Repositories.MiModulo;
using Infrastructure.Persistence.SQLDB.Main.MiModulo;

namespace Infrastructure.Repositories.MiModulo;

public sealed class MiEntidadRepository : IMiEntidadRepository
{
    private readonly MiEntidadSql _sql;

    public MiEntidadRepository(MiEntidadSql sql) => _sql = sql;

    public Task<MiEntidad?> GetByPublicIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _sql.GetByPublicIdAsync(id, cancellationToken);

    public Task<MiEntidad> InsertAsync(string nombre, CancellationToken cancellationToken = default) =>
        _sql.InsertAsync(nombre, cancellationToken);
}
```

### Paso 6 — Registrar en DI

`Infrastructure/ServiceCollectionEx.cs`:

```csharp
services.AddScoped<MiEntidadSql>();
services.AddScoped<IMiEntidadRepository, MiEntidadRepository>();
```

---

## 8. Patrones SQL avanzados

### Paginación

```sql
SELECT Id, PublicId, Nombre, CreatedAtUtc
FROM dbo.MiEntidad
WHERE EstaActivo = TRUE
ORDER BY CreatedAtUtc DESC, Id DESC
LIMIT @pageSize OFFSET @offset;
```

```csharp
new { pageSize, offset = (page - 1) * pageSize }
```

### Filtros opcionales

```sql
SELECT Id, PublicId, Nombre
FROM dbo.MiEntidad
WHERE (@nombre     IS NULL OR LOWER(Nombre)     LIKE '%' || LOWER(@nombre) || '%')
  AND (@estaActivo IS NULL OR EstaActivo = @estaActivo)
ORDER BY CreatedAtUtc DESC;
```

### CTE — último registro por grupo

```sql
WITH Ultimos AS (
    SELECT *,
           ROW_NUMBER() OVER (
               PARTITION BY GrupoId
               ORDER BY Id DESC
           ) AS Fila
    FROM dbo.MiEntidad
)
SELECT Id, PublicId, Nombre, GrupoId
FROM Ultimos
WHERE Fila = 1
  AND (@activo IS NULL OR EstaActivo = @activo);
```

### INSERT con RETURNING

```sql
INSERT INTO dbo.MiEntidad (Nombre, OtraTablaId)
VALUES (@nombre, @otraTablaId)
RETURNING Id, PublicId, Nombre, OtraTablaId, EstaActivo, CreatedAtUtc, UpdatedAtUtc;
```

### UPDATE con precondición

```sql
UPDATE dbo.MiEntidad
SET Estado       = @nuevoEstado,
    UpdatedAtUtc = timezone('utc', now())
WHERE PublicId = @publicId
  AND Estado   = @estadoEsperado;
```

Retorna `0` si el registro no existe o el estado ya cambió — el repositorio lo convierte en `NotFound` o `Conflict`.

---

## 9. Diferencias clave PostgreSQL vs SQL Server

| Patrón | SQL Server | PostgreSQL |
|--------|-----------|------------|
| Identidad | `IDENTITY(1,1)` | `GENERATED BY DEFAULT AS IDENTITY` |
| Valor al insertar | `OUTPUT inserted.*` | `RETURNING col1, col2, ...` |
| Fecha UTC | `GETUTCDATE()` | `timezone('utc', now())` |
| Top N | `SELECT TOP 1` | `LIMIT 1` |
| Paginación | `OFFSET N ROWS FETCH NEXT M ROWS ONLY` | `LIMIT M OFFSET N` |
| Parámetros | `@param` | `@param` (igual con Dapper/Npgsql) |
| UUID default | — | `DEFAULT gen_random_uuid()` |
| Concatenar texto | `+` | `\|\|` |
| Tabla temporal | `DECLARE @t TABLE (...)` | CTE con `WITH t AS (...)` |
| Existe | `IF EXISTS (SELECT 1 ...)` | `SELECT EXISTS (SELECT 1 ...)` |
| Cast | `CAST(x AS INT)` | `x::int` |

---

## 10. Tipos de datos

| Tipo .NET | Tipo PostgreSQL | Uso |
|-----------|----------------|-----|
| `long` | `BIGINT` | PKs, FKs |
| `Guid` | `UUID` | PublicId, identificadores externos |
| `string` | `VARCHAR(N)` | Texto acotado |
| `string` | `TEXT` | Texto sin límite (notas, URLs) |
| `decimal` | `NUMERIC(12,2)` | Montos, precios |
| `bool` | `BOOLEAN` | Flags activo/inactivo |
| `DateTime` | `TIMESTAMP(0)` | Fechas UTC sin microsegundos |
| `int` | `INTEGER` | Contadores, cantidades pequeñas |

---

## Checklist al agregar una entidad

- [ ] `NNN_tabla.sql` — `CREATE TABLE IF NOT EXISTS` con Id, PublicId, timestamps
- [ ] `NNN+1_tabla_indexes.sql` — `UX_` sobre PublicId + índices de FKs y filtros frecuentes
- [ ] Entidad de dominio en `Domain/Entities/{Modulo}/`
- [ ] Interfaz de repositorio en `Domain/Repositories/{Modulo}/`
- [ ] Clase `{Entidad}Sql` en `Infrastructure/Persistence/SQLDB/Main/{Modulo}/`
- [ ] Implementación del repositorio en `Infrastructure/Repositories/{Modulo}/`
- [ ] DI en `Infrastructure/ServiceCollectionEx.cs` (`AddScoped` para Sql y Repository)
- [ ] `dotnet build` pasa sin errores
