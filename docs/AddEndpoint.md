# Agregar un endpoint

Receta con un caso de uso inventado, `GetWidget`, en un modulo `{M}`. El caso real mas parecido para copiar es
`Tenancy.Application/UseCases/GetAuditLog` + `Tenancy.Presentation/Controllers/AuditLogController.cs`.

## 0. Antes de escribir

Traza el recorrido completo: controller -> handler -> repositorio -> tabla. Decide el modulo y el permiso que lo
protege. Si el permiso no existe, agregalo a `Shared.Kernel/Security/KnownPermissions.cs` **y** a
`Authentication.Domain/Rbac/RbacCatalog.cs` (con los roles que lo reciben); se siembra solo al arrancar.

## 1. Contrato: Request y Responses

```csharp
// {M}.Application/UseCases/GetWidget/GetWidgetRequest.cs
public sealed record GetWidgetRequest(Guid Id) : IRequest<GetWidgetResponse>;

// {M}.Application/UseCases/GetWidget/Responses/GetWidgetResponse.cs
public abstract record GetWidgetResponse : IResponse;
public sealed record GetWidgetSuccess(WidgetDto Data) : GetWidgetResponse, ISuccess<WidgetDto>;
public sealed record GetWidgetNotFoundFailure(string Message) : GetWidgetResponse, INotFoundFailure;
```

El request **no** lleva `TenantId`: el tenant lo aplican el filtro de EF y RLS. Si el caso de uso necesita al
actor, se lo pasa el controller (`CurrentUserPublicId`) o lo lee de `ICurrentUser`.

## 2. Handler

```csharp
internal sealed class GetWidgetHandler(IWidgetRepository widgets) : IRequestHandler<GetWidgetRequest, GetWidgetResponse>
{
    public async Task<GetWidgetResponse> Handle(GetWidgetRequest request, CancellationToken cancellationToken)
    {
        var widget = await widgets.GetByPublicIdAsync(request.Id, cancellationToken);
        return widget is null
            ? new GetWidgetNotFoundFailure("Widget no encontrado.")
            : new GetWidgetSuccess(WidgetMapping.ToDto(widget));
    }
}
```

Sin `try/catch`: una `BusinessException` la traduce el filtro global y una excepcion inesperada sale como 500
generico (el detalle va al log). Una escritura va dentro de `IUnitOfWork.ExecuteAsync` y, si es sensible, deja su
linea con `IAuditLog.Append` antes del `SaveChangesAsync`.

Registro **a mano** en `{M}.Application/ServiceCollectionEx.cs`:

```csharp
services.AddScoped<IRequestHandler<GetWidgetRequest, GetWidgetResponse>, GetWidgetHandler>();
```

## 3. Persistencia (si hay tabla nueva)

Entidad en `{M}.Domain/Entities` heredando `TenantEntity` (o `GlobalEntity` si no es de un tenant),
`IEntityTypeConfiguration<T>` en `{M}.Infrastructure/Persistence`, repositorio en `{M}.Infrastructure/Repositories`
registrado en su `ServiceCollectionEx`. Migracion y RLS: [DB.md](DB.md).

## 4. Presenter

```csharp
internal sealed class GetWidgetPresenter(ResultViewModel<WidgetsController> vm)
    : ResultPresenter<WidgetsController, GetWidgetResponse, WidgetDto>(vm);
```

`ResultPresenter` vuelca el exito o el fallo en el envelope y **lanza** si el response no es ni una cosa ni la
otra. Para dar forma propia a los datos, sobreescribe `Shape`. Registro a mano en
`{M}.Presentation/ServiceCollectionEx.cs`:

```csharp
services.AddScoped<INotificationHandler<GetWidgetResponse>, GetWidgetPresenter>();
```

## 5. Controller

```csharp
[Route("api/v1/widgets")]
public sealed class WidgetsController(IMediator mediator, ResultViewModel<WidgetsController> viewModel) : BaseApiController(mediator)
{
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.WidgetsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new GetWidgetRequest(id), cancellationToken), viewModel);
}
```

Cada accion declara su permiso. `AuthorizationSurfaceTests` falla si una accion queda solo con `[Authorize]`
(abierta a cualquier usuario del tenant) salvo que este en su lista justificada, y si cita un permiso que no esta en
el catalogo (responderia 403 a todos, en silencio). Un listado recibe `page`/`pageSize` y devuelve `PagedResult<T>`
([Pagination.md](Pagination.md)). Un endpoint anonimo sensible lleva `[EnableRateLimiting(RateLimitPolicies.Auth)]`.

## 6. Pruebas y cierre

- Unitaria del handler (NSubstitute) en `tests/{M}.Tests` o el proyecto del modulo.
- Integracion por HTTP en `tests/Api.IntegrationTests` si toca permisos, tenant o base.
- `dotnet build back-template.slnx -warnaserror` con 0 errores y 0 warnings, y `dotnet test back-template.slnx`.
