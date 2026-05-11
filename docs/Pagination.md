# Pagination.md — Paginación, Background Services y Refresh Tokens

---

## PagedResult\<T\> — respuesta paginada estandarizada

En lugar de que cada caso de uso invente su propia estructura, un tipo genérico centraliza la paginación.

### Definir PagedResult\<T\>

```csharp
// Common o Application/Shared/PagedResult.cs
public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int                    Total,
    int                    Page,
    int                    PageSize)
{
    public int  TotalPages  => (int)Math.Ceiling(Total / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPrevPage => Page > 1;
}
```

### Response del caso de uso

```csharp
// Application/UseCases/ExampleUsers/GetAll/Responses/
public abstract record GetExampleUsersResponse : IResponse;

// ISuccess (sin genérico) — evita referencia circular en JSON
public sealed record GetExampleUsersSuccess(
    IReadOnlyCollection<ExampleUserDto> Items,
    int Total, int Page, int PageSize)
    : GetExampleUsersResponse, ISuccess;

public sealed record GetExampleUsersFailure(string Message)
    : GetExampleUsersResponse, IFailure;
```

### Request con parámetros de paginación

```csharp
public sealed record GetExampleUsersRequest(
    int    Page     = 1,
    int    PageSize = 10,
    string? Search  = null)
    : IRequest<GetExampleUsersResponse>
{
    // Sanitizar en el record para que el Handler no tenga que hacerlo
    public int Page     { get; init; } = Math.Max(1, Page);
    public int PageSize { get; init; } = Math.Clamp(PageSize, 1, 100);
}
```

### Handler

```csharp
public sealed class GetExampleUsersHandler
    : IRequestHandler<GetExampleUsersRequest, GetExampleUsersResponse>
{
    private readonly IExampleUserRepository _repo;
    public GetExampleUsersHandler(IExampleUserRepository repo) => _repo = repo;

    public async Task<GetExampleUsersResponse> Handle(
        GetExampleUsersRequest request, CancellationToken ct)
    {
        var (items, total) = await _repo.GetPagedAsync(
            request.Page, request.PageSize, request.Search, ct);

        var dtos = items.Select(u => new ExampleUserDto(u)).ToList().AsReadOnly();

        return new GetExampleUsersSuccess(dtos, total, request.Page, request.PageSize);
    }
}
```

### SQL — query paginado con COUNT total

```csharp
// Infrastructure/Persistence/SQLDB/Main/ExampleUsers/ExampleUsersSql.cs
public async Task<(IEnumerable<ExampleUser> Items, int Total)> GetPagedAsync(
    int page, int pageSize, string? search, CancellationToken ct = default)
{
    var offset = (page - 1) * pageSize;

    // Un solo round-trip: datos + total con COUNT(*) OVER()
    var rows = await _db.QueryAsync<ExampleUser, int, (ExampleUser, int)>(
        """
        SELECT
            u.Id, u.PublicId, u.FullName, u.Email, u.IsActive,
            u.CreatedAtUtc, u.UpdatedAtUtc,
            COUNT(*) OVER() AS TotalCount
        FROM dbo.ExampleUsers u
        WHERE (@search IS NULL
               OR u.FullName ILIKE '%' || @search || '%'
               OR u.Email    ILIKE '%' || @search || '%')
        ORDER BY u.CreatedAtUtc DESC
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """,
        (user, total) => (user, total),
        new { search, offset, pageSize },
        splitOn: "TotalCount",
        cancellationToken: ct);

    var list = rows.ToList();
    var total = list.Count > 0 ? list[0].Item2 : 0;
    return (list.Select(r => r.Item1), total);
}

// Alternativa simple con dos queries separados:
public async Task<(IEnumerable<ExampleUser> Items, int Total)> GetPagedSimpleAsync(
    int page, int pageSize, CancellationToken ct = default)
{
    var offset = (page - 1) * pageSize;

    var items = await _db.QueryAsync<ExampleUser>(
        """
        SELECT Id, PublicId, FullName, Email, IsActive, CreatedAtUtc, UpdatedAtUtc
        FROM dbo.ExampleUsers
        ORDER BY CreatedAtUtc DESC
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """,
        new { offset, pageSize }, cancellationToken: ct);

    var total = await _db.ExecuteScalarAsync<int>(
        "SELECT COUNT(*) FROM dbo.ExampleUsers;",
        cancellationToken: ct);

    return (items, total);
}
```

### Presenter

```csharp
public sealed class GetExampleUsersPresenter : IPresenter<GetExampleUsersResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;
    public GetExampleUsersPresenter(ResultViewModel<ExampleUsersController> vm) => _viewModel = vm;

    public Task Handle(GetExampleUsersResponse notification, CancellationToken ct)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is GetExampleUsersSuccess success)
            _viewModel.OK(success);   // Data = { items, total, page, pageSize, totalPages, ... }
        return Task.CompletedTask;
    }
}
```

### Controller

```csharp
[HttpGet]
public async Task<IActionResult> GetAll(
    [FromQuery] int     page     = 1,
    [FromQuery] int     pageSize = 10,
    [FromQuery] string? search   = null,
    CancellationToken ct = default)
{
    try
    {
        _ = await Mediator.Send(new GetExampleUsersRequest(page, pageSize, search), ct);
        return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error en GetAll");
        return StatusCode(500, _viewModel.Fail(ex.Message));
    }
}
```

**Respuesta JSON:**
```json
{
  "data": {
    "items": [...],
    "total": 157,
    "page": 2,
    "pageSize": 10,
    "totalPages": 16,
    "hasNextPage": true,
    "hasPrevPage": true
  },
  "isSuccess": true,
  "message": "",
  "utcTimeStamp": "2026-05-11T..."
}
```

---

## Refresh Tokens

JWT de corta duración + refresh token de larga duración almacenado en DB.

### Migración

```sql
-- Host/Services/Schema Migration/Tables/020_refresh_tokens.sql
CREATE TABLE IF NOT EXISTS dbo.RefreshTokens (
    Id            SERIAL        PRIMARY KEY,
    UserId        INT           NOT NULL REFERENCES dbo.ExampleUsers(Id) ON DELETE CASCADE,
    Token         VARCHAR(256)  NOT NULL UNIQUE,
    ExpiresAtUtc  TIMESTAMP(0)  NOT NULL,
    IsRevoked     BOOLEAN       NOT NULL DEFAULT FALSE,
    CreatedAtUtc  TIMESTAMP(0)  NOT NULL DEFAULT (timezone('utc', now())),
    ReplacedByToken VARCHAR(256) NULL
);

-- 021_refresh_tokens_indexes.sql
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_token  ON dbo.RefreshTokens(Token);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_userid ON dbo.RefreshTokens(UserId);
```

### Entidad y repositorio

```csharp
// Domain/Entities/Auth/RefreshToken.cs
public sealed class RefreshToken
{
    public int      Id              { get; init; }
    public int      UserId          { get; init; }
    public string   Token           { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc    { get; init; }
    public bool     IsRevoked       { get; init; }
    public DateTime CreatedAtUtc    { get; init; }
    public string?  ReplacedByToken { get; init; }

    public bool IsExpired  => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsActive   => !IsRevoked && !IsExpired;
}

// Domain/Repositories/Auth/IRefreshTokenRepository.cs
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task InsertAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAsync(string token, string? replacedByToken, CancellationToken ct = default);
    Task RevokeAllForUserAsync(int userId, CancellationToken ct = default);
}
```

### Handler de refresh

```csharp
// Application/UseCases/Auth/RefreshToken/
public sealed record RefreshTokenRequest(string AccessToken, string RefreshToken)
    : IRequest<RefreshTokenResponse>;

public abstract record RefreshTokenResponse : IResponse;
public sealed record RefreshTokenSuccess(string AccessToken, string RefreshToken)
    : RefreshTokenResponse, ISuccess;
public sealed record RefreshTokenUnauthorizedFailure(string Message)
    : RefreshTokenResponse, IUnauthorizedFailure;

public sealed class RefreshTokenHandler
    : IRequestHandler<RefreshTokenRequest, RefreshTokenResponse>
{
    private readonly IRefreshTokenRepository _refreshRepo;
    private readonly IExampleUserRepository  _userRepo;
    private readonly IJwtTokenService        _jwt;

    public async Task<RefreshTokenResponse> Handle(
        RefreshTokenRequest request, CancellationToken ct)
    {
        var stored = await _refreshRepo.GetByTokenAsync(request.RefreshToken, ct);

        if (stored is null || !stored.IsActive)
            return new RefreshTokenUnauthorizedFailure("Refresh token inválido o expirado.");

        // Validar que el access token corresponde al mismo usuario
        var userId = _jwt.GetUserIdFromExpiredToken(request.AccessToken);
        if (userId != stored.UserId)
            return new RefreshTokenUnauthorizedFailure("Token no corresponde al usuario.");

        var user = await _userRepo.GetByIdAsync(stored.UserId, ct);
        if (user is null || !user.IsActive)
            return new RefreshTokenUnauthorizedFailure("Usuario no encontrado o inactivo.");

        // Rotar: revocar el viejo, crear uno nuevo
        var newRefresh = GenerateRefreshToken(user.Id);
        var newAccess  = _jwt.GenerateToken(user.PublicId, user.Email);

        await _refreshRepo.RevokeAsync(stored.Token, newRefresh.Token, ct);
        await _refreshRepo.InsertAsync(newRefresh, ct);

        return new RefreshTokenSuccess(newAccess, newRefresh.Token);
    }

    private static RefreshToken GenerateRefreshToken(int userId) => new()
    {
        UserId       = userId,
        Token        = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        CreatedAtUtc = DateTime.UtcNow
    };
}
```

---

## Background Services

`BackgroundService` para tareas recurrentes (limpiar tokens expirados, enviar emails, etc.).

### Patrón base

```csharp
// Host/Services/Background/ExpiredTokenCleanupService.cs
public sealed class ExpiredTokenCleanupService : BackgroundService
{
    // IServiceScopeFactory porque BackgroundService es Singleton
    // y necesita crear Scoped services (repositorios, DB) por cada ejecución
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
        _logger.LogInformation("ExpiredTokenCleanupService started.");

        // Esperar 1 min al arranque para que la app esté lista
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;  // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExpiredTokenCleanupService.");
                // No re-lanzar — el loop continúa en el próximo ciclo
            }

            // Repetir cada 6 horas
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }

        _logger.LogInformation("ExpiredTokenCleanupService stopped.");
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        // Crear un scope nuevo por cada ejecución — los repos son Scoped
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        var deleted = await repo.DeleteExpiredAsync(ct);
        _logger.LogInformation("Cleaned up {Count} expired refresh tokens.", deleted);
    }
}
```

### Registro en DI

```csharp
// Host/Program.cs o Host/Extensions/BackgroundServicesExtensions.cs
builder.Services.AddHostedService<ExpiredTokenCleanupService>();
```

### Regla crítica: IServiceScopeFactory

```csharp
// ❌ NUNCA inyectar Scoped en Singleton — captive dependency
public class MyBackgroundService : BackgroundService
{
    private readonly IExampleUserRepository _repo;  // Scoped inyectado en Singleton → excepción
}

// ✓ Crear un scope por cada unidad de trabajo
await using var scope = _scopeFactory.CreateAsyncScope();
var repo = scope.ServiceProvider.GetRequiredService<IExampleUserRepository>();
// usar repo dentro del scope, el scope se dispone al salir del using
```
