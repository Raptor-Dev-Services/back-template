# Testing — Guía completa

Cada módulo tiene su propio proyecto de tests: `{Modulo}.Tests`. Hay dos tipos de tests y ambos son obligatorios.

---

## Estructura de tests

```
{Modulo}.Tests/
├── Architecture/
│   └── {Modulo}ArchitectureTests.cs   ← tests de arquitectura (NetArchTest.Rules)
└── UseCases/
    ├── {Accion}HandlerTests.cs         ← tests unitarios del handler
    └── {Accion}HandlerTests.cs
```

Los proyectos que existen:

| Proyecto | Ruta |
|----------|------|
| `Users.Tests` | `Modules/Users/Users.Tests/` |
| `Tenancy.Tests` | `Modules/Tenancy/Tenancy.Tests/` |
| `Authentication.Tests` | `Shared/Authentication/Authentication.Tests/` |

---

## Correr los tests

```bash
# Un módulo específico
dotnet test back-template/Modules/Users/Users.Tests/Users.Tests.csproj
dotnet test back-template/Modules/Tenancy/Tenancy.Tests/Tenancy.Tests.csproj
dotnet test back-template/Shared/Authentication/Authentication.Tests/Authentication.Tests.csproj

# Todos a la vez (desde la solución)
dotnet test back-template/back-template.slnx

# Con output detallado
dotnet test back-template/back-template.slnx --verbosity normal

# Filtrar por nombre de método
dotnet test --filter "FullyQualifiedName~LoginHandler"

# Filtrar por categoría (Unit vs Integration)
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

---

## Tipo 1 — Tests de arquitectura (NetArchTest.Rules)

Verifican en tiempo de CI que nadie ha roto las reglas de dependencias. Si alguien referencia `Users.Infrastructure` desde `Users.Application`, el test falla y el build se rompe.

### Los 7 tests por módulo

Cada módulo tiene exactamente estos 7 tests:

```csharp
// {Modulo}.Tests/Architecture/{Modulo}ArchitectureTests.cs
using NetArchTest.Rules;
using System.Reflection;
using Users.Application.UseCases.GetUserProfile;   // cualquier tipo de Application
using Users.Domain.Entities;                        // cualquier tipo de Domain
using Users.Infrastructure.Repositories;            // cualquier tipo de Infrastructure
using Xunit;

namespace Users.Tests.Architecture;

public sealed class UsersArchitectureTests
{
    // Namespaces que se vigilan
    private const string ApplicationNs    = "Users.Application";
    private const string InfrastructureNs = "Users.Infrastructure";
    private const string PresentationNs   = "Users.Presentation";

    // Assemblies que se inspeccionan — se obtienen via typeof de cualquier clase pública
    private static readonly Assembly DomainAssembly         = typeof(UserProfile).Assembly;
    private static readonly Assembly ApplicationAssembly    = typeof(GetUserProfileHandler).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(UserProfileRepository).Assembly;

    [Fact]
    public void Domain_MustNot_DependOn_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(ApplicationNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Domain_MustNot_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Domain_MustNot_DependOn_Presentation()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(PresentationNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Application_MustNot_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Application_MustNot_DependOn_Presentation()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(PresentationNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Infrastructure_MustNot_DependOn_Application()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn(ApplicationNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Infrastructure_MustNot_DependOn_Presentation()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn(PresentationNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }
}
```

### Cómo elegir los tipos para los assemblies

Usar cualquier tipo público de cada proyecto. El assembly es lo que importa, no el tipo específico.

```csharp
// Domain: una Entity
typeof(UserProfile).Assembly

// Application: un Handler
typeof(GetUserProfileHandler).Assembly

// Infrastructure: un Repository o una clase Sql
typeof(UserProfileRepository).Assembly
// o:
typeof(UserProfilesSql).Assembly
```

### Qué pasa cuando falla

Si el test `Application_MustNot_DependOn_Infrastructure` falla, NetArchTest imprime exactamente qué clase en Application está importando Infrastructure:

```
Types that failed:
- Users.Application.UseCases.GetUserProfile.GetUserProfileHandler
  depends on: Users.Infrastructure.Persistence.SQLDB.UserProfilesSql
```

Eso señala exactamente dónde está la violación.

---

## Tipo 2 — Tests unitarios de handlers (xUnit + NSubstitute)

Verifican la lógica de negocio de cada handler de forma aislada — sin base de datos, sin HTTP, sin DI container. Son rápidos y deterministas.

### Patrón base

```csharp
// {Modulo}.Tests/UseCases/{Accion}HandlerTests.cs
using NSubstitute;
using Users.Application.UseCases.GetUserProfile;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Domain.Entities;
using Users.Domain.Repositories;
using Xunit;

namespace Users.Tests.UseCases;

public sealed class GetUserProfileHandlerTests
{
    // Los mocks se declaran como campos — se recrean por cada test (xUnit crea una instancia por [Fact])
    private readonly IUserProfileRepository _repo = Substitute.For<IUserProfileRepository>();

    [Fact]
    public async Task Handle_WhenProfileExists_ReturnsSuccess()
    {
        // Arrange — preparar el mock con datos de prueba
        var profile = new UserProfile
        {
            PublicId     = Guid.NewGuid(),
            TenantId     = 1,
            FullName     = "John Doe",
            IsActive     = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _repo.GetByPublicIdAsync(profile.PublicId, 1, Arg.Any<CancellationToken>())
             .Returns(profile);

        // Act — ejecutar el handler directamente (sin DI, sin mediator)
        var result = await new GetUserProfileHandler(_repo)
            .Handle(new GetUserProfileRequest(profile.PublicId, 1), default);

        // Assert — verificar el tipo y los datos del resultado
        var success = Assert.IsType<GetUserProfileSuccess>(result);
        Assert.NotNull(success.Data);
        Assert.Equal("John Doe", success.Data.FullName);
    }

    [Fact]
    public async Task Handle_WhenProfileNotFound_ReturnsNotFoundFailure()
    {
        // Arrange — el repo devuelve null
        _repo.GetByPublicIdAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
             .Returns((UserProfile?)null);

        // Act
        var result = await new GetUserProfileHandler(_repo)
            .Handle(new GetUserProfileRequest(Guid.NewGuid(), 1), default);

        // Assert — verificar que es el tipo de fallo correcto
        Assert.IsType<GetUserProfileNotFoundFailure>(result);
    }
}
```

### Ejemplo con múltiples dependencias — LoginHandler

Cuando el handler tiene varias interfaces, se mockean todas:

```csharp
public sealed class LoginHandlerTests
{
    private readonly IUserCredentialRepository _credentials   = Substitute.For<IUserCredentialRepository>();
    private readonly IRefreshTokenRepository   _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher           _hasher        = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService          _jwt           = Substitute.For<IJwtTokenService>();

    [Fact]
    public async Task Handle_WhenCredentialNotFound_ReturnsInvalidCredentialsFailure()
    {
        _credentials.GetForLoginAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserCredential?)null);

        var result = await new LoginHandler(_credentials, _refreshTokens, _hasher, _jwt)
            .Handle(new LoginRequest("user@test.com", "password"), default);

        Assert.IsType<LoginInvalidCredentialsFailure>(result);
    }

    [Fact]
    public async Task Handle_WhenPasswordInvalid_ReturnsInvalidCredentialsFailure()
    {
        var credential = new UserCredential
        {
            Id = 1, PublicId = Guid.NewGuid(), TenantId = 1, BranchId = 1,
            Email = "user@test.com", PasswordHash = "hash", Role = "User", IsActive = true
        };
        _credentials.GetForLoginAsync("user@test.com", Arg.Any<CancellationToken>()).Returns(credential);
        _hasher.Verify("wrong-password", "hash").Returns(false);

        var result = await new LoginHandler(_credentials, _refreshTokens, _hasher, _jwt)
            .Handle(new LoginRequest("user@test.com", "wrong-password"), default);

        Assert.IsType<LoginInvalidCredentialsFailure>(result);
    }

    [Fact]
    public async Task Handle_WhenValidCredentials_ReturnsLoginSuccess()
    {
        var credential = new UserCredential
        {
            Id = 1, PublicId = Guid.NewGuid(), TenantId = 1, BranchId = 1,
            Email = "user@test.com", PasswordHash = "hash", Role = "User", IsActive = true
        };
        _credentials.GetForLoginAsync("user@test.com", Arg.Any<CancellationToken>()).Returns(credential);
        _hasher.Verify("password", "hash").Returns(true);
        _jwt.GenerateAccessToken(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                                 Arg.Any<long>(), Arg.Any<long>())
            .Returns("access-token");
        _jwt.GenerateRefreshToken().Returns("refresh-token");
        _jwt.GetRefreshTokenExpiry().Returns(DateTime.UtcNow.AddDays(7));

        var result = await new LoginHandler(_credentials, _refreshTokens, _hasher, _jwt)
            .Handle(new LoginRequest("user@test.com", "password"), default);

        var success = Assert.IsType<LoginSuccess>(result);
        Assert.Equal("access-token", success.Data.AccessToken);
        Assert.Equal("refresh-token", success.Data.RefreshToken);
    }
}
```

---

## NSubstitute — referencia rápida

### Crear un mock

```csharp
var repo = Substitute.For<IUserProfileRepository>();
```

### Configurar retorno de un método

```csharp
// Retorno fijo
repo.GetByPublicIdAsync(publicId, tenantId, Arg.Any<CancellationToken>())
    .Returns(profile);

// Retorno null
repo.GetByPublicIdAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
    .Returns((UserProfile?)null);

// Método void / Task sin retorno — no necesita .Returns()
// Pero si quieres hacer que lance una excepción:
repo.InsertAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromException(new Exception("DB error")));
```

### Matchers — `Arg.*`

| Matcher | Qué hace |
|---------|---------|
| `Arg.Any<T>()` | Acepta cualquier valor de tipo T |
| `Arg.Is<T>(x => x > 0)` | Acepta valores que cumplen la condición |
| `Arg.Is(specificValue)` | Solo ese valor exacto |

Usar `Arg.Any<CancellationToken>()` siempre que el método reciba un CT — el test pasa `default` pero el matcher no sabe eso.

### Verificar que se llamó el método

```csharp
// Verificar que se llamó exactamente 1 vez con esos argumentos
await repo.Received(1).GetByPublicIdAsync(publicId, tenantId, Arg.Any<CancellationToken>());

// Verificar que nunca se llamó
await repo.DidNotReceive().InsertAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
```

### Comportamiento por defecto

Sin configurar, NSubstitute devuelve:
- `null` para reference types
- `0` / `false` para value types
- `Task.CompletedTask` para `Task`
- `Task.FromResult(default(T))` para `Task<T>`

Esto significa que no siempre es necesario configurar todos los métodos — solo los que el handler realmente llama en el camino que se está probando.

---

## Qué testear por handler

Regla simple: **un test por rama de lógica**.

| Condición | Test |
|-----------|------|
| Camino exitoso (happy path) | Retorna el tipo `Success` correcto con los datos esperados |
| Recurso no encontrado | Retorna `NotFoundFailure` |
| Conflicto (ya existe) | Retorna `ConflictFailure` |
| Datos inválidos | Retorna `ValidationFailure` |
| Condición de negocio especial | Un test por cada condición |

**No testear:** implementaciones de repositorios, clases SQL, serialización JSON, comportamiento HTTP — eso no es responsabilidad del handler.

---

## Tests de integración (con base de datos real)

Los tests de integración usan PostgreSQL real. Se marcan con `[Trait("Category", "Integration")]` para poder separarlos de los unit tests.

```bash
# Levantar la DB
docker compose -f compose-db.yaml up -d

# Correr solo integration tests
dotnet test --filter "Category=Integration"

# Correr solo unit tests (sin Docker)
dotnet test --filter "Category=Unit"
```

### DbFixture — conexión compartida

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Database;

namespace Users.Tests;

public sealed class DbFixture
{
    public DapperDbConnection<MainDbConnection> Db { get; }

    public DbFixture()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json")
            .AddEnvironmentVariables()
            .Build();

        var factory = new DbConnectionFactory<MainDbConnection>(config);
        Db = new DapperDbConnection<MainDbConnection>(
            factory,
            NullLogger<DapperDbConnection<MainDbConnection>>.Instance,
            config);
    }
}
```

`appsettings.Test.json` (en la raíz del proyecto `{Modulo}.Tests/`):

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

### Test de repositorio con fixture

```csharp
[Trait("Category", "Integration")]
public sealed class UserProfileRepositoryTests : IClassFixture<DbFixture>
{
    private readonly UserProfilesSql        _sql;
    private readonly UserProfileRepository  _repo;

    public UserProfileRepositoryTests(DbFixture fixture)
    {
        _sql  = new UserProfilesSql(fixture.Db);
        _repo = new UserProfileRepository(_sql);
    }

    [Fact]
    public async Task GetByPublicIdAsync_ReturnsNull_ForUnknownId()
    {
        var profile = await _repo.GetByPublicIdAsync(Guid.NewGuid(), tenantId: 1);
        Assert.Null(profile);
    }
}
```

**`IClassFixture<T>`:** xUnit crea una sola instancia de `DbFixture` para todos los tests de la clase. Así se abre la conexión una sola vez, no por cada test.

---

## Cobertura de código

```bash
# Generar datos de cobertura
dotnet test back-template/back-template.slnx --collect:"XPlat Code Coverage" --results-directory ./coverage

# Instalar la herramienta de reporte (una sola vez, global)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generar reporte HTML
reportgenerator -reports:"coverage/**/*.xml" -targetdir:"coverage/report" -reporttypes:Html

# Abrir en Windows
start coverage/report/index.html
```

---

## .csproj de un módulo Tests — referencia completa

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <!-- Necesario para resolver tipos de ASP.NET en tests de arquitectura -->
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <!-- Test runners y herramientas -->
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.5.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="10.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="NSubstitute" Version="5.3.0" />
    <PackageReference Include="NetArchTest.Rules" Version="1.3.2" />
  </ItemGroup>

  <!-- Todos los proyectos del módulo -->
  <ItemGroup>
    <ProjectReference Include="../../../Common/Common/Common.csproj" />
    <ProjectReference Include="../Users.Contracts/Users.Contracts.csproj" />
    <ProjectReference Include="../Users.Domain/Users.Domain.csproj" />
    <ProjectReference Include="../Users.Application/Users.Application.csproj" />
    <ProjectReference Include="../Users.Infrastructure/Users.Infrastructure.csproj" />
    <ProjectReference Include="../Users.Presentation/Users.Presentation.csproj" />
  </ItemGroup>
</Project>
```

---

## Convenciones

| Convención | Descripción |
|-----------|-------------|
| Nombre del método | `{Sujeto}_{Condición}_{Resultado}` — ej. `Handle_WhenProfileNotFound_ReturnsNotFoundFailure` |
| Estructura interna | Arrange / Act / Assert con línea en blanco entre secciones |
| Mocks | NSubstitute — no usar Moq para mantener consistencia |
| Handler tests | Instanciar el handler directamente con `new` — sin DI container |
| CancellationToken | Pasar `default` en el Act; usar `Arg.Any<CancellationToken>()` en el Arrange |
| Datos de prueba | Inline en el test — sin builders externos para tests unitarios simples |
| Sin SQL real | Los unit tests nunca tocan la base de datos |

---

## Resumen — qué va en cada tipo

| ¿Qué testear? | Tipo de test | Herramienta |
|---------------|-------------|-------------|
| Reglas de dependencias (Domain no toca Infrastructure, etc.) | Architecture | NetArchTest.Rules |
| Lógica del handler (happy path, not found, conflict) | Unit | xUnit + NSubstitute |
| Lógica de dominio pura (entidades con comportamiento) | Unit | xUnit |
| Queries SQL (insertar, buscar, actualizar) | Integration | xUnit + DB real |
| Endpoints HTTP completos | Integration | xUnit + WebApplicationFactory |
