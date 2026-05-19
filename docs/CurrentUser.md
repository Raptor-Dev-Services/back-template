# CurrentUser.md — Usuario Actual, Audit Trail y HttpClient Tipado

---

## Claims JWT en los controllers

Los controllers extraen `TenantId` y `BranchId` directamente de los claims del JWT. No se usa `IHttpContextAccessor` en Application.

```csharp
// En cualquier controller de Presentation
private long CurrentTenantId =>
    long.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : 0;

private long CurrentBranchId =>
    long.TryParse(User.FindFirstValue("branch_id"), out var id) ? id : 0;

private Guid CurrentUserPublicId =>
    Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : Guid.Empty;

private string CurrentEmail =>
    User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? "";

private bool IsAdmin => User.IsInRole("Admin");
```

---

## ICurrentUserService (opcional)

Para casos donde múltiples handlers necesitan el usuario autenticado, se puede crear `ICurrentUserService`.

### Interfaz en Application

```csharp
// {Modulo}.Application/Abstractions/ICurrentUserService.cs
public interface ICurrentUserService
{
    Guid?   UserPublicId     { get; }
    string? Email            { get; }
    long    TenantId         { get; }
    long    BranchId         { get; }
    bool    IsAuthenticated  { get; }
    bool    IsInRole(string role);
}
```

### Implementación en Presentation (conoce HttpContext)

```csharp
// {Modulo}.Presentation/Services/CurrentUserService.cs
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    public Guid? UserPublicId =>
        Guid.TryParse(User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public string? Email => User?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public long TenantId =>
        long.TryParse(User?.FindFirstValue("tenant_id"), out var id) ? id : 0;

    public long BranchId =>
        long.TryParse(User?.FindFirstValue("branch_id"), out var id) ? id : 0;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
```

### Registro en DI

```csharp
// {Modulo}.Presentation/ServiceCollectionEx.cs
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserService, CurrentUserService>();
```

### Uso en Handlers

```csharp
public sealed class UpdateMyProfileHandler : IRequestHandler<UpdateMyProfileRequest, UpdateMyProfileResponse>
{
    private readonly IUserProfileRepository _profiles;
    private readonly ICurrentUserService    _currentUser;

    public UpdateMyProfileHandler(IUserProfileRepository profiles, ICurrentUserService currentUser)
    {
        _profiles    = profiles;
        _currentUser = currentUser;
    }

    public async Task<UpdateMyProfileResponse> Handle(UpdateMyProfileRequest request, CancellationToken ct)
    {
        if (_currentUser.UserPublicId != request.PublicId && !_currentUser.IsInRole("Admin"))
            return new UpdateMyProfileForbiddenFailure("No autorizado.");

        var profile = await _profiles.GetByPublicIdAsync(request.PublicId, _currentUser.TenantId, ct);
        if (profile is null)
            return new UpdateMyProfileNotFoundFailure("Perfil no encontrado.");

        return new UpdateMyProfileSuccess(/* ... */);
    }
}
```

---

## Roles y Claims

### Claims emitidos al generar el token

`Authentication.Infrastructure/Services/JwtTokenService.cs` genera los claims:

```csharp
new Claim(JwtRegisteredClaimNames.Sub,   credential.PublicId.ToString()),
new Claim(JwtRegisteredClaimNames.Email, credential.Email),
new Claim(ClaimTypes.Role,               credential.Role),
new Claim("tenant_id",                   credential.TenantId.ToString()),
new Claim("branch_id",                   credential.BranchId.ToString()),
new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
```

### Verificar roles en controladores

```csharp
// Declarativo — aplica a todo el endpoint
[HttpDelete("{id:guid}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Disable(Guid id, CancellationToken ct) { ... }

// Programático — verificación en el handler
if (!_currentUser.IsInRole("Admin") && _currentUser.UserPublicId != resource.OwnerPublicId)
    return new ForbiddenFailure("No tienes permiso para acceder a este recurso.");
```

---

## Audit Trail — CreatedBy / UpdatedBy

Para registrar qué usuario creó o modificó cada entidad, pasar el `UserPublicId` como parte del request desde el controller.

### En el controller

```csharp
[HttpPost]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Create([FromBody] CreateProductBody body, CancellationToken ct = default)
{
    _ = await _mediator.Send(new CreateProductRequest(
        body.Name, CurrentTenantId, CurrentBranchId, CurrentUserPublicId), ct);
    return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
}
```

### En el request

```csharp
public sealed record CreateProductRequest(
    string Name,
    long   TenantId,
    long   BranchId,
    Guid   CreatedByPublicId)
    : IRequest<CreateProductResponse>;
```

### En la tabla SQL

```sql
ALTER TABLE dbo.Products
    ADD COLUMN IF NOT EXISTS CreatedByPublicId UUID NULL,
    ADD COLUMN IF NOT EXISTS UpdatedByPublicId UUID NULL;
```

---

## HttpClient Tipado + Resiliencia

Para llamar a APIs externas con retry + circuit breaker.

### Interfaz en Application

```csharp
// {Modulo}.Application/Abstractions/IExternalPaymentService.cs
public interface IExternalPaymentService
{
    Task<PaymentResult> ChargeAsync(string cardToken, decimal amount, CancellationToken ct);
}
```

### Implementación en Infrastructure

```csharp
// {Modulo}.Infrastructure/ExternalServices/ExternalPaymentService.cs
public sealed class ExternalPaymentService : IExternalPaymentService
{
    private readonly HttpClient _http;
    private readonly ILogger<ExternalPaymentService> _logger;

    public ExternalPaymentService(HttpClient http, ILogger<ExternalPaymentService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<PaymentResult> ChargeAsync(string cardToken, decimal amount, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("/api/charge", new { cardToken, amount }, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Payment failed: {Status}", response.StatusCode);
            return PaymentResult.Failed("Cargo fallido.");
        }
        return await response.Content.ReadFromJsonAsync<PaymentResult>(ct)
               ?? PaymentResult.Failed("Respuesta vacía.");
    }
}
```

### Registro en DI

```csharp
// {Modulo}.Infrastructure/ServiceCollectionEx.cs
services.AddHttpClient<IExternalPaymentService, ExternalPaymentService>(client =>
{
    client.BaseAddress = new Uri(configuration["ExternalServices:PaymentUrl"]!);
    client.Timeout     = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler();   // retry + circuit breaker de Microsoft.Extensions.Http.Resilience
```

### Uso en un Handler

```csharp
public async Task<ProcessPaymentResponse> Handle(ProcessPaymentRequest request, CancellationToken ct)
{
    var result = await _payments.ChargeAsync(request.CardToken, request.Amount, ct);
    if (!result.IsSuccess)
        return new ProcessPaymentFailure(result.ErrorMessage);

    return new ProcessPaymentSuccess(result.TransactionId);
}
```
