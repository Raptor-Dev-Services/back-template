# Módulos — Ciclo de vida completo

Guía de referencia para crear, acoplar, desacoplar y extraer módulos en este monolito modular.

---

## ¿Qué es un módulo?

Un módulo es una unidad funcional autónoma compuesta de **6 proyectos `.csproj`** independientes. Sus límites son reales en tiempo de compilación: un módulo no puede referenciar el interior de otro módulo.

```
{Modulo}.Contracts/     ← tipos POCO públicos — lo único visible entre módulos
{Modulo}.Domain/        ← entidades + interfaces de repositorios
{Modulo}.Application/   ← casos de uso (Request, Handler, Responses, DTOs)
{Modulo}.Infrastructure/← implementaciones (SQL, repos, servicios externos)
{Modulo}.Presentation/  ← Controllers, Presenters, RequestBodies
{Modulo}.Tests/         ← tests unitarios + tests de arquitectura
```

**Dirección de dependencias:**

```
Contracts      → (sin dependencias de proyecto)
Domain         → Common
Application    → Common + Domain + Contracts
Infrastructure → Common + Domain + Shared.Database        (NO referencia Application)
Presentation   → Common + Application + Shared.Web        (NO referencia Infrastructure)
Tests          → todos los anteriores + xUnit + NSubstitute + NetArchTest.Rules
```

---

## 1. Agregar un nuevo módulo

Se usa `Inventory` como ejemplo. Reemplazar con el nombre real del módulo.

### Paso 1 — Crear la estructura de carpetas

```
Modules/
└── Inventory/
    ├── Inventory.Contracts/
    ├── Inventory.Domain/
    ├── Inventory.Application/
    ├── Inventory.Infrastructure/
    ├── Inventory.Presentation/
    └── Inventory.Tests/
```

### Paso 2 — Crear los 6 .csproj

**`Inventory.Contracts/Inventory.Contracts.csproj`** — sin dependencias externas:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

**`Inventory.Domain/Inventory.Domain.csproj`** — solo Common:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../../Common/Common/Common.csproj" />
  </ItemGroup>
</Project>
```

**`Inventory.Application/Inventory.Application.csproj`** — Common + Domain + Contracts:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../../Common/Common/Common.csproj" />
    <ProjectReference Include="../Inventory.Contracts/Inventory.Contracts.csproj" />
    <ProjectReference Include="../Inventory.Domain/Inventory.Domain.csproj" />
  </ItemGroup>
</Project>
```

**`Inventory.Infrastructure/Inventory.Infrastructure.csproj`** — Common + Domain + Shared.Database:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../../Common/Common/Common.csproj" />
    <ProjectReference Include="../../../Shared/Database/Shared.Database.csproj" />
    <ProjectReference Include="../Inventory.Domain/Inventory.Domain.csproj" />
  </ItemGroup>
</Project>
```

**`Inventory.Presentation/Inventory.Presentation.csproj`** — Common + Application + Shared.Web:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../../Common/Common/Common.csproj" />
    <ProjectReference Include="../../../Shared/Web/Shared.Web.csproj" />
    <ProjectReference Include="../Inventory.Application/Inventory.Application.csproj" />
  </ItemGroup>
</Project>
```

**`Inventory.Tests/Inventory.Tests.csproj`** — todos los proyectos del módulo + xUnit + NSubstitute + NetArchTest:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
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
  <ItemGroup>
    <ProjectReference Include="../../../Common/Common/Common.csproj" />
    <ProjectReference Include="../Inventory.Contracts/Inventory.Contracts.csproj" />
    <ProjectReference Include="../Inventory.Domain/Inventory.Domain.csproj" />
    <ProjectReference Include="../Inventory.Application/Inventory.Application.csproj" />
    <ProjectReference Include="../Inventory.Infrastructure/Inventory.Infrastructure.csproj" />
    <ProjectReference Include="../Inventory.Presentation/Inventory.Presentation.csproj" />
  </ItemGroup>
</Project>
```

### Paso 3 — Estructura interna mínima por capa

**Contracts:**
```
Inventory.Contracts/
└── Events/
    ├── IInventoryEvent.cs
    └── ItemCreatedEvent.cs
```

```csharp
// IInventoryEvent.cs
namespace Inventory.Contracts.Events;
public interface IInventoryEvent { DateTime OccurredOnUtc { get; } }

// ItemCreatedEvent.cs
namespace Inventory.Contracts.Events;
public sealed record ItemCreatedEvent(long ItemId, DateTime OccurredOnUtc) : IInventoryEvent;
```

**Domain:**
```
Inventory.Domain/
├── Entities/
│   └── Item.cs
└── Repositories/
    └── IItemRepository.cs
```

**Application:**
```
Inventory.Application/
├── Dto/
│   └── ItemDto.cs
├── UseCases/
│   └── GetItem/
│       ├── GetItemRequest.cs
│       ├── GetItemHandler.cs
│       └── Responses/
│           ├── GetItemResponse.cs
│           ├── GetItemSuccess.cs
│           └── GetItemNotFoundFailure.cs
└── ServiceCollectionEx.cs
```

**Infrastructure:**
```
Inventory.Infrastructure/
├── Persistence/
│   └── SQLDB/
│       └── ItemsSql.cs
├── Repositories/
│   └── ItemRepository.cs
└── ServiceCollectionEx.cs
```

**Presentation:**
```
Inventory.Presentation/
├── Controllers/
│   └── InventoryController.cs
├── Presenters/
│   └── GetItemPresenter.cs
├── RequestBodies/
│   └── CreateItemBody.cs
└── ServiceCollectionEx.cs
```

**Tests:**
```
Inventory.Tests/
├── Architecture/
│   └── InventoryArchitectureTests.cs
└── UseCases/
    └── GetItemHandlerTests.cs
```

### Paso 4 — ServiceCollectionEx por capa

**Application (`Inventory.Application/ServiceCollectionEx.cs`):**
```csharp
namespace Inventory.Application;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddInventoryApplicationServices(this IServiceCollection services)
        => services; // AddMediator se llama desde Host — no aquí
}
```

**Infrastructure (`Inventory.Infrastructure/ServiceCollectionEx.cs`):**
```csharp
public static IServiceCollection AddInventoryInfrastructureServices(this IServiceCollection services)
{
    services.AddScoped<ItemsSql>();
    services.AddScoped<IItemRepository, ItemRepository>();
    return services;
}
```

**Presentation (`Inventory.Presentation/ServiceCollectionEx.cs`):**
```csharp
public static IServiceCollection AddInventoryPresentationServices(this IServiceCollection services)
{
    services.AddScoped(typeof(ResultViewModel<>));

    // Presenters registrados manualmente — nunca vía AddMediator
    services.AddScoped<INotificationHandler<GetItemResponse>, GetItemPresenter>();

    services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
    return services;
}
```

### Paso 5 — Tests de arquitectura

```csharp
// Inventory.Tests/Architecture/InventoryArchitectureTests.cs
using NetArchTest.Rules;

public sealed class InventoryArchitectureTests
{
    private static readonly Assembly Domain         = typeof(Item).Assembly;
    private static readonly Assembly Application    = typeof(GetItemHandler).Assembly;
    private static readonly Assembly Infrastructure = typeof(ItemRepository).Assembly;
    private static readonly Assembly Presentation   = typeof(InventoryController).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_Application() =>
        Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOn("Inventory.Application")
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Domain_should_not_depend_on_Infrastructure() =>
        Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOn("Inventory.Infrastructure")
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Application_should_not_depend_on_Infrastructure() =>
        Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOn("Inventory.Infrastructure")
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Application_should_not_depend_on_Presentation() =>
        Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOn("Inventory.Presentation")
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Infrastructure_should_not_depend_on_Application() =>
        Types.InAssembly(Infrastructure).ShouldNot()
            .HaveDependencyOn("Inventory.Application")
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Infrastructure_should_not_depend_on_Presentation() =>
        Types.InAssembly(Infrastructure).ShouldNot()
            .HaveDependencyOn("Inventory.Presentation")
            .GetResult().IsSuccessful.Should().BeTrue();

    [Fact]
    public void Presentation_should_not_depend_on_Infrastructure() =>
        Types.InAssembly(Presentation).ShouldNot()
            .HaveDependencyOn("Inventory.Infrastructure")
            .GetResult().IsSuccessful.Should().BeTrue();
}
```

### Paso 6 — Registrar en back-template.slnx

```xml
<Folder Name="/Inventory/">
  <Project Path="Modules/Inventory/Inventory.Contracts/Inventory.Contracts.csproj" />
  <Project Path="Modules/Inventory/Inventory.Domain/Inventory.Domain.csproj" />
  <Project Path="Modules/Inventory/Inventory.Application/Inventory.Application.csproj" />
  <Project Path="Modules/Inventory/Inventory.Infrastructure/Inventory.Infrastructure.csproj" />
  <Project Path="Modules/Inventory/Inventory.Presentation/Inventory.Presentation.csproj" />
  <Project Path="Modules/Inventory/Inventory.Tests/Inventory.Tests.csproj" />
</Folder>
```

---

## 2. Acoplar un módulo a Host.Api

### Host.Api.csproj — agregar referencias

Solo se referencian Application, Infrastructure y Presentation. Nunca Domain, Contracts ni Tests desde Host.

```xml
<ProjectReference Include="../Modules/Inventory/Inventory.Application/Inventory.Application.csproj" />
<ProjectReference Include="../Modules/Inventory/Inventory.Infrastructure/Inventory.Infrastructure.csproj" />
<ProjectReference Include="../Modules/Inventory/Inventory.Presentation/Inventory.Presentation.csproj" />
```

### Program.cs — registrar servicios

```csharp
// 1. Pasar el ensamblado Application a AddMediator (una sola llamada)
builder.Services.AddMediator(
    typeof(Tenancy.Application.ServiceCollectionEx).Assembly,
    typeof(Users.Application.ServiceCollectionEx).Assembly,
    typeof(Authentication.Application.ServiceCollectionEx).Assembly,
    typeof(Inventory.Application.ServiceCollectionEx).Assembly   // ← agregar
);

// 2. Registrar los 3 servicios del módulo
builder.Services.AddInventoryApplicationServices();
builder.Services.AddInventoryInfrastructureServices();
builder.Services.AddInventoryPresentationServices();

// 3. Si el módulo tiene hubs SignalR u otros endpoints especiales:
// app.MapInventoryModule();
```

**Por qué AddMediator recibe todos los ensamblados en una sola llamada:** `AddMediator` registra `IPipelineBehavior<,> → InteractorPipeline<,>`. Si se llama N veces, el pipeline se encadena N veces y cada handler se ejecuta N veces. Una llamada, todos los ensamblados.

---

## 3. Desacoplar un módulo de Host.Api

Proceso inverso al de acoplar. El módulo sigue existiendo en el repositorio, solo deja de estar activo en el Host.

### 1. Remover referencias en Host.Api.csproj

Eliminar las 3 `<ProjectReference>` del módulo (Application, Infrastructure, Presentation).

### 2. Remover de Program.cs

- Quitar el ensamblado `.Application` del `AddMediator(...)`
- Quitar las 3 llamadas `Add{Modulo}*Services()`
- Quitar `app.Map{Modulo}Module()` si existía

### 3. Remover del back-template.slnx (opcional)

Eliminar el `<Folder>` del módulo o comentarlo si se va a re-acoplar pronto.

### 4. Build de verificación

```bash
dotnet build Host.Api/Host.Api.csproj
```

Si hay errores de compilación, algún otro módulo tiene una referencia al módulo removido. Revisar todos los `.csproj` de módulos existentes — ninguno debería referenciar algo que no sea `{Modulo}.Contracts`.

---

## 4. Extraer un módulo a su propio repositorio Git

Cuando un módulo necesita versionado independiente, o debe compartirse entre múltiples monolitos.

### Paso 1 — Crear el repositorio del módulo

```bash
mkdir inventory-module
cd inventory-module
git init
git remote add origin https://github.com/tu-org/inventory-module.git
```

### Paso 2 — Copiar el módulo

```bash
cp -r path/to/back-template/Modules/Inventory/ ./Modules/Inventory/
```

### Paso 3 — Agregar Common como submódulo

`Common` contiene las abstracciones base. **Siempre como submódulo, nunca como copia.**

```bash
git submodule add https://github.com/Raptor-Dev-Services/Common Common/Common
git submodule update --init --recursive
```

### Paso 4 — Manejar Shared.Database y Shared.Web

Tres opciones, en orden de recomendación según contexto:

**Opción A — Submódulo Git (si Shared evolucionará o se comparte entre +2 repos):**
```bash
# Si Database y Web están en un repo propio de Shared:
git submodule add https://github.com/tu-org/shared-infrastructure Shared
git submodule update --init --recursive
```

**Opción B — Copia como código fuente (para módulos standalone que no cambian Shared):**
```bash
cp -r path/to/back-template/Shared/Database/ ./Shared/Database/
cp -r path/to/back-template/Shared/Web/ ./Shared/Web/
```

**Opción C — Paquete NuGet privado (para equipos con Azure Artifacts o GitHub Packages):**
Publicar `Shared.Database` y `Shared.Web` en el feed privado y referenciarlos como `<PackageReference>`.

### Paso 5 — Ajustar rutas de ProjectReference

Después de copiar, los caminos relativos en los `.csproj` apuntarán a rutas que ya no existen. Actualizar para reflejar la nueva estructura de carpetas del repo del módulo.

Ejemplo: en el repo original `Inventory.Infrastructure.csproj` tiene:
```xml
<ProjectReference Include="../../../Common/Common/Common.csproj" />
<ProjectReference Include="../../../Shared/Database/Shared.Database.csproj" />
```

En el repo extraído con la estructura `Common/Common/` y `Shared/Database/` en la raíz:
```xml
<ProjectReference Include="../../../Common/Common/Common.csproj" />
<ProjectReference Include="../../../Shared/Database/Shared.Database.csproj" />
```
(sin cambios si la estructura espeja la original — verificar caso a caso)

### Paso 6 — Crear solución standalone

```xml
<!-- inventory-module.slnx -->
<Solution>
  <Folder Name="/Inventory/">
    <Project Path="Modules/Inventory/Inventory.Contracts/Inventory.Contracts.csproj" />
    <Project Path="Modules/Inventory/Inventory.Domain/Inventory.Domain.csproj" />
    <Project Path="Modules/Inventory/Inventory.Application/Inventory.Application.csproj" />
    <Project Path="Modules/Inventory/Inventory.Infrastructure/Inventory.Infrastructure.csproj" />
    <Project Path="Modules/Inventory/Inventory.Presentation/Inventory.Presentation.csproj" />
    <Project Path="Modules/Inventory/Inventory.Tests/Inventory.Tests.csproj" />
  </Folder>
  <Folder Name="/Shared/">
    <Project Path="Shared/Database/Shared.Database.csproj" />
    <Project Path="Shared/Web/Shared.Web.csproj" />
  </Folder>
  <Project Path="Common/Common/Common.csproj" />
</Solution>
```

### Paso 7 — Push inicial

```bash
git add .
git commit -m "feat: initial extraction of Inventory module"
git push -u origin main
```

---

## 5. Usar un módulo extraído como submódulo Git

### En el monolito consumidor — agregar el submódulo

```bash
git submodule add https://github.com/tu-org/inventory-module Modules/Inventory
git submodule update --init --recursive
```

Esto descarga el repositorio del módulo en `Modules/Inventory/`. La estructura interna es la misma que cuando el módulo era parte del monolito.

### Referenciar los proyectos en el monolito consumidor

**Host.Api.csproj:**
```xml
<ProjectReference Include="../Modules/Inventory/Modules/Inventory/Inventory.Application/Inventory.Application.csproj" />
<ProjectReference Include="../Modules/Inventory/Modules/Inventory/Inventory.Infrastructure/Inventory.Infrastructure.csproj" />
<ProjectReference Include="../Modules/Inventory/Modules/Inventory/Inventory.Presentation/Inventory.Presentation.csproj" />
```

**back-template.slnx:**
```xml
<Folder Name="/Inventory/">
  <Project Path="Modules/Inventory/Modules/Inventory/Inventory.Contracts/Inventory.Contracts.csproj" />
  <Project Path="Modules/Inventory/Modules/Inventory/Inventory.Domain/Inventory.Domain.csproj" />
  <Project Path="Modules/Inventory/Modules/Inventory/Inventory.Application/Inventory.Application.csproj" />
  <Project Path="Modules/Inventory/Modules/Inventory/Inventory.Infrastructure/Inventory.Infrastructure.csproj" />
  <Project Path="Modules/Inventory/Modules/Inventory/Inventory.Presentation/Inventory.Presentation.csproj" />
  <Project Path="Modules/Inventory/Modules/Inventory/Inventory.Tests/Inventory.Tests.csproj" />
</Folder>
```

**Program.cs** — idéntico al caso de módulo local:
```csharp
builder.Services.AddMediator(
    // ... otros módulos
    typeof(Inventory.Application.ServiceCollectionEx).Assembly
);
builder.Services.AddInventoryApplicationServices();
builder.Services.AddInventoryInfrastructureServices();
builder.Services.AddInventoryPresentationServices();
```

### Actualizar el submódulo a una versión nueva

```bash
# Actualizar Inventory a la última versión de su rama por defecto
git submodule update --remote --merge Modules/Inventory

# Fijar el cambio en el monolito
git add Modules/Inventory
git commit -m "chore: bump Inventory module to latest"
```

### Fijar una versión específica (tag o commit)

```bash
cd Modules/Inventory
git checkout v2.1.0
cd ../..
git add Modules/Inventory
git commit -m "chore: pin Inventory module to v2.1.0"
```

---

## 6. Dependencias compartidas entre repos

### Common — siempre submódulo Git

`Common` define los contratos base (`IMediator`, `IRequest<>`, `IResponse`, `ISuccess<>`, etc.). Todos los repos deben apuntar al mismo SHA para que los tipos sean compatibles. **Nunca copiar Common — siempre submódulo.**

Si dos módulos submódulo en el mismo monolito apuntan a SHAs distintos de `Common`, habrá errores de compilación por incompatibilidad de tipos. Al actualizar `Common`, actualizar todos los submódulos de módulos en la misma PR.

### Shared.Database — según contexto

`Shared.Database` contiene `DapperDbConnection<T>`, `DbConnectionFactory<T>` y los marcadores de BD (`MainDbConnection`, `ReadonlyDbConnection`).

| Escenario | Estrategia |
|-----------|-----------|
| Módulo standalone sin más consumidores | Copia como código fuente |
| Módulo compartido entre 2+ monolitos | Submódulo Git o paquete NuGet privado |
| Equipo dedicado con CI/CD de paquetes | Paquete NuGet privado (Azure Artifacts / GitHub Packages) |

Si el módulo define sus propios marcadores de BD (e.g., `InventoryDbConnection`) distintos de los del monolito, agregarlos en `Shared.Database` del módulo sin afectar al monolito — los marcadores son clases vacías sin lógica.

### Shared.Web — copia o submódulo

`Shared.Web` solo contiene `BaseApiController` (unas pocas líneas). Es pequeño y cambia raramente.

- Si tienes un repo propio de `Shared`: incluirlo ahí y usarlo como submódulo.
- Si no: copiar el archivo directamente. El costo de mantener la copia en sync es mínimo dado su tamaño.

---

## Resumen — comandos Git de referencia

```bash
# Clonar un repo con todos sus submódulos
git clone --recurse-submodules https://github.com/tu-org/back-template.git

# Inicializar submódulos después de un clone sin --recurse-submodules
git submodule update --init --recursive

# Ver estado de todos los submódulos (SHA fijado vs SHA real)
git submodule status

# Agregar un módulo como submódulo
git submodule add https://github.com/tu-org/inventory-module Modules/Inventory
git submodule update --init --recursive

# Actualizar un submódulo específico a su rama remota
git submodule update --remote --merge Modules/Inventory

# Actualizar todos los submódulos
git submodule update --remote --merge

# Eliminar un submódulo completamente
git submodule deinit Modules/Inventory
git rm Modules/Inventory
rm -rf .git/modules/Modules/Inventory
git commit -m "chore: remove Inventory submodule"
```
