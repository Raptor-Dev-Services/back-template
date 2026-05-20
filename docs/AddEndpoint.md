# AddEndpoint.md — Guía para agregar un endpoint

Cubre dos escenarios completos:

- **Caso A** — La entidad no existe aún: crear entidad → configuración EF Core → repositorio → caso de uso → presenter → controller → test
- **Caso B** — La entidad ya existe en la BD: empezar directo en el repositorio

Ejemplo de módulo: `Products` con entidad `Product`.

---

## Estructura de un módulo (6 proyectos)

```
Modules/{Modulo}/
├── {Modulo}.Contracts/       → DTOs + Integration Events (sin dependencias)
├── {Modulo}.Domain/          → Entidades + interfaces de repositorio (sin dependencias)
├── {Modulo}.Application/     → Handlers + Use Cases (→ Common + Domain + Contracts)
├── {Modulo}.Infrastructure/  → Repositorios con AppDbContext (→ Common + Domain + Shared.Database)
├── {Modulo}.Presentation/    → Controllers + Presenters (→ Common + Application + Shared.Web)
└── {Modulo}.Tests/           → Tests arquitectura + unitarios
```

Módulos compartidos (Auth) viven en `Shared/{Modulo}/` con la misma estructura.

---

## CASO A — Entidad nueva: desde el dominio hasta el controller

### Paso 1 — Entidad de dominio

`Products.Domain/Entities/Product.cs`

```csharp
namespace Products.Domain.Entities;

public sealed class Product
{
    public long     Id           { get; init; }
    public Guid     PublicId     { get; init; }
    public long     TenantId     { get; init; }
    public string   Name         { get; init; } = string.Empty;
    public bool     IsActive     { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
```

---

### Paso 2 — Interfaz de repositorio

`Products.Domain/Repositories/IProductRepository.cs`

```csharp
using Products.Domain.Entities;

namespace Products.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default);
    Task<long> InsertAsync(Guid publicId, long tenantId, string name, CancellationToken ct = default);
}
```

Solo declara la firma — sin nada de EF Core ni conexiones.

---

### Paso 3 — Configuración EF Core

`Shared/Database/EntityTypeConfigurations/ProductConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Products.Domain.Entities;

namespace Shared.Database.EntityTypeConfigurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).UseIdentityByDefaultColumn();
        b.Property(e => e.PublicId).HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.CreatedAtUtc)
            .HasColumnType("timestamp(0)")
            .HasDefaultValueSql("timezone('utc', now())");
        b.Property(e => e.UpdatedAtUtc)
            .HasColumnType("timestamp(0)")
            .HasDefaultValueSql("timezone('utc', now())");
        b.HasIndex(e => e.PublicId).IsUnique();
    }
}
```

---

### Paso 4 — Agregar DbSet en AppDbContext

`Shared/Database/AppDbContext.cs` — agregar la línea:

```csharp
public DbSet<Product> Products { get; set; } = null!;
```

Si la entidad necesita filtro de tenant, agregar en `OnModelCreating`:

```csharp
mb.Entity<Product>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
```

---

### Paso 5 — DTO en Contracts

`Products.Contracts/Dtos/ProductDto.cs`

```csharp
namespace Products.Contracts.Dtos;

public sealed record ProductDto(Guid PublicId, string Name, bool IsActive);
```

Los DTOs viven en `.Contracts` — nunca en `.Application`. Son los tipos que otros módulos pueden consumir.

---

### Paso 6 — Implementación del repositorio

`Products.Infrastructure/Repositories/ProductRepository.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Products.Domain.Entities;
using Products.Domain.Repositories;
using Shared.Database;

namespace Products.Infrastructure.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;
    public ProductRepository(AppDbContext db) => _db = db;

    public async Task<Product?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.PublicId == publicId && e.IsActive, ct);

    public async Task<long> InsertAsync(Guid publicId, long tenantId, string name, CancellationToken ct = default)
    {
        var entity = new Product
        {
            PublicId = publicId,
            TenantId = tenantId,
            Name     = name,
            IsActive = true
        };
        _db.Products.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }
}
```

`Products.Infrastructure/ServiceCollectionEx.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Products.Domain.Repositories;
using Products.Infrastructure.Repositories;

namespace Products.Infrastructure;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddProductsInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        return services;
    }
}
```

---

### Paso 7 — Caso de uso (Request + Handler + Responses)

`Products.Application/UseCases/GetProduct/GetProductRequest.cs`

```csharp
using Common.Messaging;
using Products.Application.UseCases.GetProduct.Responses;

namespace Products.Application.UseCases.GetProduct;

public sealed record GetProductRequest(Guid PublicId, long TenantId)
    : IRequest<GetProductResponse>;
```

`Products.Application/UseCases/GetProduct/Responses/GetProductResponse.cs`

```csharp
using Common.Messaging;

namespace Products.Application.UseCases.GetProduct.Responses;

public abstract record GetProductResponse : IResponse;
```

`Products.Application/UseCases/GetProduct/Responses/GetProductSuccess.cs`

```csharp
using Common.Results;
using Products.Contracts.Dtos;

namespace Products.Application.UseCases.GetProduct.Responses;

public sealed record GetProductSuccess(ProductDto Data)
    : GetProductResponse, ISuccess<ProductDto>;
```

`Products.Application/UseCases/GetProduct/Responses/GetProductNotFoundFailure.cs`

```csharp
using Common.Results;

namespace Products.Application.UseCases.GetProduct.Responses;

public sealed record GetProductNotFoundFailure(string Message)
    : GetProductResponse, INotFoundFailure;
```

`Products.Application/UseCases/GetProduct/GetProductHandler.cs`

```csharp
using Common.Messaging;
using Products.Application.UseCases.GetProduct.Responses;
using Products.Contracts.Dtos;
using Products.Domain.Repositories;

namespace Products.Application.UseCases.GetProduct;

public sealed class GetProductHandler : IRequestHandler<GetProductRequest, GetProductResponse>
{
    private readonly IProductRepository _products;
    public GetProductHandler(IProductRepository products) => _products = products;

    public async Task<GetProductResponse> Handle(
        GetProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByPublicIdAsync(request.PublicId, cancellationToken);

        if (product is null)
            return new GetProductNotFoundFailure("Producto no encontrado.");

        return new GetProductSuccess(new ProductDto(product.PublicId, product.Name, product.IsActive));
    }
}
```

> El handler es descubierto automáticamente por `AddMediator()` — **no** se registra manualmente.

---

### Paso 8 — Presenter

`Products.Presentation/Presenters/GetProductPresenter.cs`

```csharp
using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using Products.Application.UseCases.GetProduct.Responses;
using Products.Contracts.Dtos;
using Products.Presentation.Controllers;

namespace Products.Presentation.Presenters;

public sealed class GetProductPresenter : INotificationHandler<GetProductResponse>
{
    private readonly ResultViewModel<ProductsController> _viewModel;
    public GetProductPresenter(ResultViewModel<ProductsController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(GetProductResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<ProductDto> success)
            _viewModel.Set(success);
        return Task.CompletedTask;
    }
}
```

---

### Paso 9 — Controller

`Products.Presentation/Controllers/ProductsController.cs`

```csharp
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Products.Application.UseCases.GetProduct;
using Products.Application.UseCases.GetProduct.Responses;
using Shared.Web;
using System.Security.Claims;

namespace Products.Presentation.Controllers;

[Route("api/products")]
[Authorize]
public sealed class ProductsController : BaseApiController
{
    private readonly ILogger<ProductsController>         _logger;
    private readonly ResultViewModel<ProductsController> _viewModel;

    public ProductsController(
        IMediator mediator,
        ILogger<ProductsController> logger,
        ResultViewModel<ProductsController> viewModel) : base(mediator)
    {
        _logger    = logger;
        _viewModel = viewModel;
    }

    private long CurrentTenantId =>
        long.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : 0;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(new GetProductRequest(id, CurrentTenantId), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is GetProductNotFoundFailure ? NotFound(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById Product");
            var inner = ex; while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }
}
```

> El controller extiende `BaseApiController` de `Shared.Web` — expone `Mediator` como `protected`. No hay `[ApiController]` en el controller porque ya está en `BaseApiController`.

---

### Paso 10 — DI en Presentation

`Products.Presentation/ServiceCollectionEx.cs`

```csharp
using Common.Messaging;
using Common.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Products.Application.UseCases.GetProduct.Responses;
using Products.Presentation.Presenters;
using System.Reflection;

namespace Products.Presentation;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddProductsWebApiServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(ResultViewModel<>));
        services.AddScoped<INotificationHandler<GetProductResponse>, GetProductPresenter>();
        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
        return services;
    }
}
```

> Presenters se registran **manualmente** — nunca en el scan de `AddMediator`. Esto previene doble invocación.

---

### Paso 11 — Registrar en Host.Api

`Host.Api/Host.Api.csproj` — agregar references al nuevo módulo:

```xml
<ProjectReference Include="../Modules/Products/Products.Application/Products.Application.csproj" />
<ProjectReference Include="../Modules/Products/Products.Infrastructure/Products.Infrastructure.csproj" />
<ProjectReference Include="../Modules/Products/Products.Presentation/Products.Presentation.csproj" />
```

`Host.Api/Program.cs` — agregar al startup:

```csharp
builder.Services.AddMediator(
    typeof(Tenancy.Application.ServiceCollectionEx).Assembly,
    typeof(Users.Application.ServiceCollectionEx).Assembly,
    typeof(Authentication.Application.ServiceCollectionEx).Assembly,
    typeof(Products.Application.ServiceCollectionEx).Assembly   // ← nuevo
);

builder.Services.AddProductsInfrastructureServices();
builder.Services.AddProductsWebApiServices();
```

---

### Paso 12 — Test unitario del handler

`Products.Tests/UseCases/GetProductHandlerTests.cs`

```csharp
using NSubstitute;
using Products.Application.UseCases.GetProduct;
using Products.Application.UseCases.GetProduct.Responses;
using Products.Domain.Entities;
using Products.Domain.Repositories;
using Xunit;

namespace Products.Tests.UseCases;

public sealed class GetProductHandlerTests
{
    private readonly IProductRepository _repo = Substitute.For<IProductRepository>();

    [Fact]
    public async Task Handle_WhenProductExists_ReturnsSuccess()
    {
        var product = new Product { PublicId = Guid.NewGuid(), TenantId = 1, Name = "Widget", IsActive = true };
        _repo.GetByPublicIdAsync(product.PublicId, Arg.Any<CancellationToken>()).Returns(product);

        var result = await new GetProductHandler(_repo)
            .Handle(new GetProductRequest(product.PublicId, 1), default);

        var success = Assert.IsType<GetProductSuccess>(result);
        Assert.Equal("Widget", success.Data.Name);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFoundFailure()
    {
        _repo.GetByPublicIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        var result = await new GetProductHandler(_repo)
            .Handle(new GetProductRequest(Guid.NewGuid(), 1), default);

        Assert.IsType<GetProductNotFoundFailure>(result);
    }
}
```

---

## CASO B — Entidad ya existe: desde el repositorio hasta el controller

Cuando la entidad ya está en la BD (EF Core ya la tiene en `AppDbContext`), saltarse los pasos 1-4 del Caso A y comenzar directamente en el repositorio. Los pasos son idénticos al Caso A del paso 6 en adelante.

**Ejemplo:** la tabla `dbo.user_profiles` ya existe — agregar un nuevo endpoint `GET /api/users/{id}/summary`.

### Paso 1 — Agregar método a la interfaz del repositorio

`Users.Domain/Repositories/IUserProfileRepository.cs`:

```csharp
Task<UserProfileSummary?> GetSummaryByPublicIdAsync(Guid publicId, CancellationToken ct = default);
```

Si necesitas una proyección diferente (no la entidad completa):

```csharp
// Users.Domain/Projections/UserProfileSummary.cs
public sealed class UserProfileSummary
{
    public Guid   PublicId { get; init; }
    public string FullName { get; init; } = string.Empty;
}
```

### Paso 2 — Implementar en el repositorio

`Users.Infrastructure/Repositories/UserProfileRepository.cs`:

```csharp
public async Task<UserProfileSummary?> GetSummaryByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
    await _db.UserProfiles
        .AsNoTracking()
        .Where(e => e.PublicId == publicId && e.IsActive)
        .Select(e => new UserProfileSummary { PublicId = e.PublicId, FullName = e.FullName })
        .FirstOrDefaultAsync(ct);
```

> No hay cambios en DI — `UserProfileRepository` ya está registrado.

### Pasos 3-8 — Idénticos al Caso A (pasos 7-12)

Crear el DTO en Contracts → Request → Handler → Responses → Presenter → agregar endpoint al controller existente → registrar presenter en ServiceCollectionEx → test unitario.

---

## Variantes de respuesta del Presenter

### Variante A — Éxito con dato único (`ISuccess<TDto>`)

```csharp
// Success: GetProductSuccess(ProductDto Data) : ..., ISuccess<ProductDto>
else if (notification is ISuccess<ProductDto> success)
    _viewModel.Set(success);   // Data = success.Data
```

### Variante B — Éxito con colección o datos custom (`ISuccess`)

```csharp
// Success: GetProductsSuccess(IReadOnlyCollection<ProductDto> Items, int Total, ...) : ..., ISuccess
else if (notification is GetProductsSuccess success)
    _viewModel.OK(success);   // Data = el record completo
```

> **Nunca** `ISuccess<TSelf>` donde `Data => this` — crea referencia circular en la serialización JSON.

### Variante C — Éxito sin datos (update, disable)

```csharp
else if (notification is ISuccess)
    _viewModel.OK(new { });   // Data = {}
```

---

## Lifetimes en DI

| Tipo | Lifetime | Razón |
|------|----------|-------|
| `AppDbContext` | Scoped | Una instancia por request HTTP |
| `I{Entidad}Repository` | Scoped | Depende de `AppDbContext` |
| `IRequestHandler<,>` | Scoped (auto) | `AddMediator` los registra automático |
| `INotificationHandler<>` (Presenters) | Scoped | Comparte `ResultViewModel` con el controller |
| `ResultViewModel<>` | Scoped | Compartido entre controller y presenter del mismo request |

---

## Checklist pre-build

### Contracts
- [ ] DTO en `{Modulo}.Contracts/Dtos/` — nunca en Application
- [ ] Sin dependencias externas en el proyecto Contracts

### Domain
- [ ] Entidad con `Id`, `PublicId`, `TenantId`, campos de negocio, timestamps UTC
- [ ] Interfaz de repositorio en `{Modulo}.Domain/Repositories/`
- [ ] Sin referencias a Infrastructure, Application o Presentation

### Shared.Database
- [ ] `{Entidad}Configuration.cs` en `EntityTypeConfigurations/` — tabla, columnas, índices, FK
- [ ] `DbSet<{Entidad}>` agregado en `AppDbContext`
- [ ] Si tiene TenantId, `HasQueryFilter` agregado en `OnModelCreating`

### Infrastructure
- [ ] Repositorio implementa la interfaz de dominio
- [ ] Repositorio inyecta `AppDbContext` por constructor
- [ ] `AsNoTracking()` en todas las lecturas
- [ ] `ExecuteUpdateAsync()` para actualizaciones (evita cargar + re-guardar entidades `init`)
- [ ] `AddScoped<I{Entidad}Repository, {Entidad}Repository>()` en DI

### Application
- [ ] `{Accion}Request.cs` — `sealed record`, implementa `IRequest<{Accion}Response>`
- [ ] `Responses/{Accion}Response.cs` — `abstract record`, implementa `IResponse`
- [ ] Success y Failure en `Responses/`
- [ ] Handler no tiene referencias a Presentation o Infrastructure
- [ ] Handler no se registra manualmente

### Presentation
- [ ] Presenter registrado **manualmente** en `ServiceCollectionEx.cs`
- [ ] Controller extiende `BaseApiController` (de `Shared.Web`) — no `ControllerBase`
- [ ] Controller inyecta `IMediator`, `ILogger<T>` y `ResultViewModel<TController>`
- [ ] `TenantId` extraído de `User.FindFirstValue("tenant_id")`
- [ ] Usa `Mediator.Send(...)` (del base) — no `_mediator.Send(...)`

### Tests
- [ ] Test unitario del handler con NSubstitute (caso éxito + caso not found)
- [ ] Sin acceso a base de datos real en tests unitarios

### Host.Api
- [ ] Assembly del nuevo `.Application` en `AddMediator()` (solo si es módulo nuevo)
- [ ] `{Modulo}.Infrastructure.csproj`, `.Application.csproj`, `.Presentation.csproj` referenciados en `Host.Api.csproj`

### Build final
- [ ] `dotnet build Host.Api/Host.Api.csproj` → **0 errores**
