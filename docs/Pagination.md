# Pagination.md — Paginación, Background Services y Refresh Tokens

---

## Paginación estandarizada

### Response del caso de uso

```csharp
// {Modulo}.Application/UseCases/GetUserProfiles/Responses/
public abstract record GetUserProfilesResponse : IResponse;

// ISuccess sin genérico — evita referencia circular en JSON
public sealed record GetUserProfilesSuccess(
    IReadOnlyCollection<UserProfileDto> Items,
    int Total, int Page, int PageSize)
    : GetUserProfilesResponse, ISuccess;
```

### Request con parámetros de paginación

```csharp
public sealed record GetUserProfilesRequest(
    int Page     = 1,
    int PageSize = 20)
    : IRequest<GetUserProfilesResponse>
{
    public int Page     { get; init; } = Math.Max(1, Page);
    public int PageSize { get; init; } = Math.Clamp(PageSize, 1, 100);
}
```

### Handler

```csharp
public sealed class GetUserProfilesHandler : IRequestHandler<GetUserProfilesRequest, GetUserProfilesResponse>
{
    private readonly IUserProfileRepository _profiles;
    public GetUserProfilesHandler(IUserProfileRepository profiles) => _profiles = profiles;

    public async Task<GetUserProfilesResponse> Handle(GetUserProfilesRequest request, CancellationToken ct)
    {
        var items = await _profiles.GetPagedAsync(request.Page, request.PageSize, ct);
        var total = await _profiles.GetCountAsync(ct);

        var dtos = items.Select(p => new UserProfileDto(p.PublicId, p.FullName, p.IsActive, p.CreatedAtUtc, p.UpdatedAtUtc))
                        .ToList().AsReadOnly();

        return new GetUserProfilesSuccess(dtos, total, request.Page, request.PageSize);
    }
}
```

### EF Core — implementación real del repositorio

```csharp
// Users.Infrastructure/Repositories/UserProfileRepository.cs
public async Task<IReadOnlyCollection<UserProfile>> GetPagedAsync(
    int page, int pageSize, CancellationToken ct = default) =>
    await _db.UserProfiles
        .AsNoTracking()
        .Where(p => p.IsActive)
        .OrderByDescending(p => p.CreatedAtUtc)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);

public async Task<int> GetCountAsync(CancellationToken ct = default) =>
    await _db.UserProfiles
        .AsNoTracking()
        .CountAsync(p => p.IsActive, ct);
```

> El global query filter del `AppDbContext` filtra automáticamente por `TenantId` — el repositorio no recibe ni pasa `tenantId` explícitamente.

El handler llama ambos en paralelo si los resultados son independientes, o secuencial si el total no es crítico:

```csharp
// Secuencial (más simple)
var items = await _profiles.GetPagedAsync(request.Page, request.PageSize, ct);
var total = await _profiles.GetCountAsync(ct);

// Paralelo (más eficiente)
var itemsTask = _profiles.GetPagedAsync(request.Page, request.PageSize, ct);
var totalTask = _profiles.GetCountAsync(ct);
await Task.WhenAll(itemsTask, totalTask);
var items = await itemsTask;
var total = await totalTask;
```

### Presenter

```csharp
public sealed class GetUserProfilesPresenter : INotificationHandler<GetUserProfilesResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;
    public GetUserProfilesPresenter(ResultViewModel<UsersController> viewModel) => _viewModel = viewModel;

    public Task Handle(GetUserProfilesResponse notification, CancellationToken ct)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is GetUserProfilesSuccess success)
            _viewModel.OK(success);   // Data = { items, total, page, pageSize }
        return Task.CompletedTask;
    }
}
```

### Controller

```csharp
[HttpGet]
public async Task<IActionResult> GetAll(
    [FromQuery] int page     = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
{
    try
    {
        _ = await _mediator.Send(new GetUserProfilesRequest(CurrentTenantId, page, pageSize), ct);
        return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error en GetAll UserProfiles");
        var inner = ex;
        while (inner.InnerException != null) inner = inner.InnerException!;
        return StatusCode(500, _viewModel.Fail(inner.Message));
    }
}
```

**Respuesta JSON:**

```json
{
  "data": {
    "items": [...],
    "total": 57,
    "page": 2,
    "pageSize": 20
  },
  "isSuccess": true,
  "message": null,
  "utcTimeStamp": "2026-05-17T..."
}
```

---

## Refresh Tokens

JWT de corta duración (`ExpirationMinutes`) + refresh token de larga duración (`RefreshTokenExpiryDays`) almacenado en `dbo.RefreshTokens`.

La FK de `dbo.RefreshTokens` apunta a `dbo.Credentials(Id)`, no a usuarios de perfil.

### EntityTypeConfiguration

`Authentication.Infrastructure` registra el esquema vía `RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>`:

- FK a `dbo.credentials(Id)` — `HasForeignKey(t => t.CredentialId)`
- `Token` único — `HasIndex(t => t.Token).IsUnique()`
- `IsRevoked` default `false`, `CreatedAtUtc` default `timezone('utc', now())`

### Entidad de dominio

`Authentication.Domain/Entities/RefreshToken.cs`:

```csharp
public sealed class RefreshToken
{
    public long     Id           { get; init; }
    public long     CredentialId { get; init; }
    public string   Token        { get; init; } = "";
    public DateTime ExpiresAtUtc { get; init; }
    public bool     IsRevoked    { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsActive  => !IsRevoked && !IsExpired;
}
```

### Flujo de refresh

`Authentication.Application/UseCases/RefreshToken/RefreshTokenHandler.cs`:

1. Busca el token en `dbo.RefreshTokens` por valor exacto
2. Verifica que `IsActive` (no revocado, no expirado)
3. Carga las credenciales por `CredentialId`
4. Revoca el token viejo (`IsRevoked = true`)
5. Genera nuevo access token + nuevo refresh token
6. Guarda el nuevo refresh token

### EF Core — repositorio

```csharp
// Authentication.Infrastructure/Repositories/RefreshTokenRepository.cs
public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default) =>
    await _db.RefreshTokens
        .AsNoTracking()
        .IgnoreQueryFilters()           // sin query filter de tenant (RefreshTokens no lo tienen)
        .FirstOrDefaultAsync(t => t.Token == token && !t.IsRevoked, ct);

public async Task InsertAsync(long credentialId, string token, DateTime expiresAtUtc, CancellationToken ct = default)
{
    _db.RefreshTokens.Add(new RefreshToken
    {
        CredentialId = credentialId,
        Token        = token,
        ExpiresAtUtc = expiresAtUtc
    });
    await _db.SaveChangesAsync(ct);
}

public async Task RevokeAsync(string token, CancellationToken ct = default) =>
    await _db.RefreshTokens
        .Where(t => t.Token == token)
        .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);
```

---

## Background Services

`BackgroundService` para tareas recurrentes (limpiar tokens expirados, etc.).

### Patrón base

```csharp
// Host.Api/Services/Background/ExpiredTokenCleanupService.cs
public sealed class ExpiredTokenCleanupService : BackgroundService
{
    // IServiceScopeFactory porque BackgroundService es Singleton
    // y necesita crear Scoped services (repos, DB) por cada ejecución
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredTokenCleanupService> _logger;

    public ExpiredTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExpiredTokenCleanupService.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var deleted = await repo.DeleteExpiredAsync(ct);
        _logger.LogInformation("Cleaned up {Count} expired refresh tokens.", deleted);
    }
}
```

### Registro

```csharp
// Host.Api/Program.cs
builder.Services.AddHostedService<ExpiredTokenCleanupService>();
```

### Regla crítica: nunca inyectar Scoped en Singleton

```csharp
// ❌ NUNCA — captive dependency
public class MyBackgroundService : BackgroundService
{
    private readonly IRefreshTokenRepository _repo;  // Scoped inyectado en Singleton → excepción
}

// ✓ Crear scope por cada unidad de trabajo
await using var scope = _scopeFactory.CreateAsyncScope();
var repo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
```
