# Testing.md — Guía de Tests

Cómo escribir y correr tests en este proyecto con xUnit.

---

## Estructura del proyecto de tests

```
Tests/
├── Domain/
│   └── SampleDomainTests.cs        ← Tests de entidades y lógica de dominio pura
├── Application/
│   └── UseCases/{Modulo}/          ← Tests de handlers (unit tests)
├── Infrastructure/
│   └── {Modulo}/                   ← Tests de repositorios (integration tests — requieren DB)
└── WebApi/
    └── {Modulo}/                   ← Tests de presenters y mapeo de ViewModels
```

---

## Correr los tests

```bash
# Todos los tests
dotnet test back-template/Tests/Tests.csproj

# Con verbosidad para ver cada test
dotnet test back-template/Tests/Tests.csproj --verbosity normal

# Filtrar por nombre
dotnet test --filter "FullyQualifiedName~GetProductHandler"

# Filtrar por categoría (si usas [Trait])
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"

# Con cobertura de código
dotnet test back-template/Tests/Tests.csproj --collect:"XPlat Code Coverage"
```

---

## Testear un Handler (unit test)

Los handlers son la parte más importante de testear. No tienen dependencias en ASP.NET ni en la base de datos — solo en repositorios que puedes mockear.

### Ejemplo: GetProductHandler

```csharp
using Application.Dto.Products;
using Application.UseCases.Products.GetProduct;
using Domain.Entities.Products;
using Domain.Repositories.Products;
using NSubstitute;   // o Moq — agregar el paquete NuGet que prefieras
using Xunit;

namespace Tests.Application.UseCases.Products;

public class GetProductHandlerTests
{
    private readonly IProductRepository _repo = Substitute.For<IProductRepository>();
    private readonly GetProductHandler  _handler;

    public GetProductHandlerTests()
    {
        _handler = new GetProductHandler(_repo);
    }

    [Fact]
    public async Task Handle_existing_product_returns_success()
    {
        // Arrange
        var publicId = Guid.NewGuid();
        var product  = new Product
        {
            Id          = 1,
            PublicId    = publicId,
            Name        = "Widget",
            Description = "A widget",
            Price       = 9.99m,
            IsActive    = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _repo.GetByPublicIdAsync(publicId, Arg.Any<CancellationToken>())
             .Returns(product);

        // Act
        var response = await _handler.Handle(
            new GetProductRequest(publicId), CancellationToken.None);

        // Assert
        var success = Assert.IsType<GetProductSuccess>(response);
        Assert.Equal(publicId, success.Data.ProductId);
        Assert.Equal("Widget", success.Data.Name);
    }

    [Fact]
    public async Task Handle_missing_product_returns_not_found()
    {
        // Arrange
        _repo.GetByPublicIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((Product?)null);

        // Act
        var response = await _handler.Handle(
            new GetProductRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        var failure = Assert.IsType<GetProductNotFoundFailure>(response);
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
    }

    [Fact]
    public async Task Handle_calls_repository_with_correct_publicId()
    {
        // Arrange
        var publicId = Guid.NewGuid();
        _repo.GetByPublicIdAsync(publicId, Arg.Any<CancellationToken>())
             .Returns((Product?)null);

        // Act
        await _handler.Handle(new GetProductRequest(publicId), CancellationToken.None);

        // Assert
        await _repo.Received(1).GetByPublicIdAsync(publicId, Arg.Any<CancellationToken>());
    }
}
```

### Agregar NSubstitute (o Moq)

Editar `Tests/Tests.csproj` y agregar:

```xml
<!-- NSubstitute (recomendado — sintaxis más limpia) -->
<PackageReference Include="NSubstitute" Version="5.3.0" />

<!-- O Moq (alternativa popular) -->
<PackageReference Include="Moq" Version="4.20.72" />
```

---

## Testear un Presenter (unit test)

```csharp
using Application.Dto.Products;
using Application.UseCases.Products.GetProduct;
using Common.Results;
using Common.ViewModels;
using WebApi.EndPoints.Products.Presenters;
using Xunit;

namespace Tests.WebApi.Products;

public class GetProductPresenterTests
{
    private readonly ResultViewModel<object> _viewModel = new();
    private readonly GetProductPresenter     _presenter;

    public GetProductPresenterTests()
    {
        // ResultViewModel<T> no tiene dependencias — se crea directamente
        // El tipo genérico no importa para los tests
        _presenter = new GetProductPresenter(
            (ResultViewModel<WebApi.EndPoints.Products.ProductsController>)(object)_viewModel);
    }

    [Fact]
    public async Task Handle_success_sets_data_in_viewmodel()
    {
        var dto      = new ProductDto(Guid.NewGuid(), "Widget", "Desc", 9.99m, true, DateTime.UtcNow, DateTime.UtcNow);
        var response = new GetProductSuccess(dto);

        await _presenter.Handle(response, CancellationToken.None);

        Assert.True(_viewModel.IsSuccess);
        Assert.Equal(dto, _viewModel.Data);
    }

    [Fact]
    public async Task Handle_failure_sets_error_in_viewmodel()
    {
        var response = new GetProductNotFoundFailure("Producto no encontrado.");

        await _presenter.Handle(response, CancellationToken.None);

        Assert.False(_viewModel.IsSuccess);
        Assert.Equal("Producto no encontrado.", _viewModel.Message);
    }
}
```

---

## Testear lógica de dominio pura (unit test)

Las entidades de dominio son simples records — se testean directamente sin mocks.

```csharp
using Domain.Entities.Products;
using Xunit;

namespace Tests.Domain;

public class ProductTests
{
    [Fact]
    public void Product_should_be_active_by_default()
    {
        var product = new Product { IsActive = true };
        Assert.True(product.IsActive);
    }
}
```

---

## Tests de integración (con base de datos real)

Los tests de repositorios requieren PostgreSQL real. Usar `compose-db.yaml` para levantar la base de datos antes de correrlos.

### Configuración

Agregar en `Tests/Tests.csproj`:
```xml
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
```

Crear `Tests/appsettings.Test.json`:
```json
{
  "ConnectionStrings": {
    "MainDbConnection": "Host=localhost;Port=5432;Database=back_template_test;Username=postgres;Password=postgres"
  },
  "CustomLogging": {
    "IncludeSqlText": false
  }
}
```

### Fixture de base de datos

```csharp
using Infrastructure.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests.Infrastructure;

public sealed class DbFixture : IDisposable
{
    public MainDapperDbConnection Db { get; }

    public DbFixture()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json")
            .Build();

        var factory = new MainDbConnectionFactory(config);
        Db = new MainDapperDbConnection(
            factory,
            NullLogger<MainDapperDbConnection>.Instance,
            config);
    }

    public void Dispose() { }
}
```

### Test de repositorio

```csharp
using Infrastructure.Persistence.SQLDB.Main.Products;
using Infrastructure.Repositories.Products;
using Xunit;

namespace Tests.Infrastructure.Products;

[Trait("Category", "Integration")]
public class ProductRepositoryTests : IClassFixture<DbFixture>
{
    private readonly ProductsSql        _sql;
    private readonly ProductRepository  _repo;

    public ProductRepositoryTests(DbFixture fixture)
    {
        _sql  = new ProductsSql(fixture.Db);
        _repo = new ProductRepository(_sql);
    }

    [Fact]
    public async Task InsertAsync_creates_product_with_generated_publicId()
    {
        var product = await _repo.InsertAsync("Test Widget", "Description", 19.99m);

        Assert.NotEqual(Guid.Empty, product.PublicId);
        Assert.Equal("Test Widget", product.Name);
        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task GetByPublicIdAsync_returns_null_for_unknown_id()
    {
        var product = await _repo.GetByPublicIdAsync(Guid.NewGuid());

        Assert.Null(product);
    }
}
```

Levantar la DB antes de correr los integration tests:
```bash
docker compose -f compose-db.yaml up -d
dotnet test --filter "Category=Integration"
```

---

## Separar unit tests e integration tests

Usar `[Trait]` para categorizar:

```csharp
[Trait("Category", "Unit")]
public class GetProductHandlerTests { ... }

[Trait("Category", "Integration")]
public class ProductRepositoryTests { ... }
```

Correr solo unit tests (no requieren Docker):
```bash
dotnet test --filter "Category=Unit"
```

Correr solo integration tests (requieren Docker con compose-db.yaml):
```bash
docker compose -f compose-db.yaml up -d
dotnet test --filter "Category=Integration"
```

---

## Cobertura de código

```bash
# Generar reporte de cobertura
dotnet test back-template/Tests/Tests.csproj --collect:"XPlat Code Coverage" --results-directory ./coverage

# Ver el archivo generado
ls coverage/**/*.xml

# Instalar reportgenerator (una vez)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generar reporte HTML
reportgenerator -reports:"coverage/**/*.xml" -targetdir:"coverage/report" -reporttypes:Html

# Abrir el reporte
start coverage/report/index.html   # Windows
```

---

## Convenciones para tests

| Convención | Descripción |
|-----------|-------------|
| Nombre del método | `{Sujeto}_{Condición}_{Resultado}` — ej. `Handle_missing_product_returns_not_found` |
| Estructura del test | Arrange / Act / Assert |
| Mocking | NSubstitute o Moq — no mockear la base de datos en unit tests |
| Tests de handlers | Solo testear lógica de negocio — sin SQL real |
| Tests de repositorios | Usar base de datos real (integration) — no mockear Dapper |
| Limpieza de datos | Usar transacciones que se revierten al final del test, o una base de datos de test separada |
