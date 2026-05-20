# DB.md — Gestión de Base de Datos

Guía de referencia para la capa de datos en este proyecto. Todo acceso a datos pasa por `AppDbContext` de `Shared/Database`.

---

## Cadena de acceso a datos

```
ConnectionStrings:MainDbConnection  (appsettings.json)
    ↓
AppDbContext                        (EF Core DbContext — Scoped)
    ↓
{Entidad}Repository                 (implementa interfaz de dominio — inyecta AppDbContext)
    ↓
Handler                             (lógica de negocio)
```

**Regla absoluta:** los repositorios son la única capa que toca `AppDbContext`. Cero acceso a datos en handlers o servicios de aplicación.

---

## AppDbContext

Vive en `Shared/Database/AppDbContext.cs`. Contiene todos los `DbSet<T>` del sistema y aplica global query filters de multi-tenancy.

```csharp
public sealed class AppDbContext : DbContext
{
    private readonly ITenantContextAccessor _tenantAccessor;

    public DbSet<Tenant>         Tenants       { get; set; } = null!;
    public DbSet<UserCredential> Credentials   { get; set; } = null!;
    public DbSet<RefreshToken>   RefreshTokens { get; set; } = null!;
    public DbSet<UserProfile>    UserProfiles  { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContextAccessor tenantAccessor)
        : base(options) { _tenantAccessor = tenantAccessor; }

    private long CurrentTenantId =>
        long.TryParse(_tenantAccessor.Current?.TenantId, out var id) ? id : 0L;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("dbo");
        mb.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        mb.Entity<UserCredential>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        mb.Entity<UserProfile>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }
}
```

Registro en `Shared.Database/ServiceCollectionEx.cs`:

```csharp
public static IServiceCollection AddMainDatabase(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(configuration.GetConnectionString("MainDbConnection")));
    services.AddHostedService<DatabaseInitializationService>();
    return services;
}
```

Llamado en `Host.Api/Program.cs`:

```csharp
builder.Services.AddMainDatabase(builder.Configuration);
```

---

## EntityTypeConfigurations

Cada entidad tiene su archivo `IEntityTypeConfiguration<T>` en `Shared/Database/EntityTypeConfigurations/`. EF Core los descubre automáticamente vía `ApplyConfigurationsFromAssembly`.

**Convenciones:**
- Tabla en `snake_case` (ej. `user_profiles`), esquema `dbo`.
- Columnas en `PascalCase` (convención EF Core por defecto).
- PKs con `UseIdentityByDefaultColumn()`.
- UUIDs con `HasDefaultValueSql("gen_random_uuid()")`.
- Timestamps con `HasDefaultValueSql("timezone('utc', now())")` y `HasColumnType("timestamp(0)")`.
- Índices únicos y FKs declarados aquí.

```csharp
public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("user_profiles");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).UseIdentityByDefaultColumn();
        b.Property(e => e.PublicId).HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        b.Property(e => e.CreatedAtUtc)
            .HasColumnType("timestamp(0)")
            .HasDefaultValueSql("timezone('utc', now())");
        b.HasIndex(e => e.PublicId).IsUnique();
        b.HasOne<Tenant>().WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

## Patrones de repositorio con EF Core

### Lectura

```csharp
// Único registro — AsNoTracking() para lecturas que no van a modificarse
public async Task<UserProfile?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
    await _db.UserProfiles
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.PublicId == publicId && e.IsActive, ct);

// Lista paginada
public async Task<List<UserProfile>> GetAllAsync(int page, int pageSize, CancellationToken ct = default) =>
    await _db.UserProfiles
        .AsNoTracking()
        .OrderByDescending(e => e.CreatedAtUtc)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);
```

### Insertar

Entidades con propiedades `init` se insertan con object initializer — EF Core materializa sin necesidad de setters:

```csharp
public async Task<long> InsertAsync(
    Guid publicId, long tenantId, string fullName, CancellationToken ct = default)
{
    var entity = new UserProfile
    {
        PublicId  = publicId,
        TenantId  = tenantId,
        FullName  = fullName,
        IsActive  = true
    };
    _db.UserProfiles.Add(entity);
    await _db.SaveChangesAsync(ct);
    return entity.Id;
}
```

### Actualizar (propiedades `init`)

`ExecuteUpdateAsync` evita cargar la entidad y funciona con `init` porque opera a nivel SQL:

```csharp
public async Task UpdateAsync(Guid publicId, string fullName, CancellationToken ct = default) =>
    await _db.UserProfiles
        .Where(e => e.PublicId == publicId && e.IsActive)
        .ExecuteUpdateAsync(s => s
            .SetProperty(e => e.FullName, fullName)
            .SetProperty(e => e.UpdatedAtUtc, DateTime.UtcNow),
        ct);
```

### Soft delete

```csharp
public async Task DisableAsync(Guid publicId, CancellationToken ct = default) =>
    await _db.UserProfiles
        .Where(e => e.PublicId == publicId)
        .ExecuteUpdateAsync(s => s
            .SetProperty(e => e.IsActive, false)
            .SetProperty(e => e.UpdatedAtUtc, DateTime.UtcNow),
        ct);
```

### IgnoreQueryFilters — auth sin tenant

Login y refresh no tienen tenant en el JWT aún. El repositorio de credenciales usa `IgnoreQueryFilters()`:

```csharp
public async Task<UserCredential?> GetForLoginAsync(string email, CancellationToken ct = default) =>
    await _db.Credentials
        .IgnoreQueryFilters()
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.Email == email && e.IsActive, ct);
```

---

## Esquema de BD actual

| Tabla | Módulo dueño | Descripción |
|-------|-------------|-------------|
| `dbo.tenants` | Tenancy | Empresas SaaS |
| `dbo.credentials` | Authentication | Login (Email, PasswordHash, Role, TenantId) |
| `dbo.user_profiles` | Users | Datos de perfil (PublicId, FullName, TenantId) |
| `dbo.refresh_tokens` | Authentication | Tokens JWT con FK a credentials |

> **Separación importante:** `dbo.credentials` es propiedad del módulo `Authentication`. `dbo.user_profiles` es propiedad del módulo `Users`. La relación entre ellas se maneja por Integration Events, no por FK directa.

---

## Agregar una entidad nueva

1. Crear la entidad en `{Modulo}.Domain/Entities/`.
2. Crear `{Entidad}Configuration.cs` en `Shared/Database/EntityTypeConfigurations/`.
3. Agregar `DbSet<{Entidad}> {Entidades} { get; set; } = null!;` en `AppDbContext`.
4. Si la entidad necesita filtro de tenant, agregar `HasQueryFilter` en `OnModelCreating`.
5. La tabla se crea automáticamente al reiniciar la API (`EnsureCreatedAsync`).

> **Advertencia:** `EnsureCreated` no ejecuta migraciones. Si la tabla ya existe con un esquema diferente, no la altera. Para cambios de esquema en producción usar `dotnet ef migrations` o scripts SQL manuales.

---

## Inicialización del esquema

`DatabaseInitializationService` (en `Shared.Database/ServiceCollectionEx.cs`) es un `IHostedService` que:
1. Crea un scope DI al iniciar.
2. Establece un `TenantContext("0")` dummy para que los filtros de EF Core no fallen.
3. Llama `db.Database.EnsureCreatedAsync()` — crea las tablas según las configuraciones.

Esto reemplaza el antiguo sistema de archivos `.sql` + `SchemaMigrationHostedService`.

---

## Transacciones

Para operaciones que deben ser atómicas entre múltiples `DbSet`:

```csharp
public async Task OperacionAtomicaAsync(CancellationToken ct = default)
{
    await using var transaction = await _db.Database.BeginTransactionAsync(ct);
    try
    {
        _db.Credentials.Add(credential);
        await _db.SaveChangesAsync(ct);

        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
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

## Diferencias PostgreSQL útiles

| Patrón | SQL Server | PostgreSQL / EF Core Npgsql |
|--------|-----------|----------------------------|
| Identidad | `IDENTITY(1,1)` | `UseIdentityByDefaultColumn()` |
| UUID default | — | `HasDefaultValueSql("gen_random_uuid()")` |
| Fecha UTC | `GETUTCDATE()` | `HasDefaultValueSql("timezone('utc', now())")` |
| Tipo timestamp sin ms | `datetime2(0)` | `HasColumnType("timestamp(0)")` |
| Exists | `IF EXISTS (...)` | `SELECT EXISTS (...)` |

---

## DI — Lifetimes

| Clase | Lifetime |
|-------|---------|
| `AppDbContext` | Scoped (registrado por `AddDbContext`) |
| `{Entidad}Repository` | Scoped |
