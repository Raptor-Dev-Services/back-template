# CurrentUser.md — Usuario Actual, Audit Trail y HttpClient Tipado

---

## ICurrentUserService

Forma de obtener el usuario autenticado dentro de los Handlers **sin pasar `HttpContext`** a la capa de Application.

### El problema

```csharp
// ❌ Application no puede conocer HttpContext — viola Clean Architecture
public class UpdateUserHandler
{
    private readonly IHttpContextAccessor _http;  // ← Infrastructure de ASP.NET Core en Application

    public async Task<...> Handle(...)
    {
        var userId = _http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
```

### Interfaz en Application

```csharp
// Application/Abstractions/ICurrentUserService.cs
public interface ICurrentUserService
{
    Guid?   UserId    { get; }
    string? Email     { get; }
    bool    IsAuthenticated { get; }
    bool    IsInRole(string role);
    string? GetClaim(string claimType);
}
```

### Implementación en WebApi (conoce HttpContext)

```csharp
// WebApi/Services/CurrentUserService.cs
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(
        User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    public string? GetClaim(string claimType) => User?.FindFirstValue(claimType);
}
```

### Registro en DI

```csharp
// WebApi/ServiceCollectionEx.cs
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserService, CurrentUserService>();
```

### Uso en Handlers

```csharp
// Application — solo conoce ICurrentUserService, no HttpContext
public sealed class UpdateExampleUserHandler
    : IRequestHandler<UpdateExampleUserRequest, UpdateExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;
    private readonly ICurrentUserService    _currentUser;

    public UpdateExampleUserHandler(
        IExampleUserRepository repo,
        ICurrentUserService currentUser)
    {
        _repo        = repo;
        _currentUser = currentUser;
    }

    public async Task<UpdateExampleUserResponse> Handle(
        UpdateExampleUserRequest request, CancellationToken ct)
    {
        // Verificar que el usuario está modificando su propio perfil
        // (o es admin y puede modificar cualquiera)
        if (_currentUser.UserId != request.PublicId && !_currentUser.IsInRole("admin"))
            return new UpdateExampleUserUnauthorizedFailure("No autorizado.");

        var user = await _repo.GetByPublicIdAsync(request.PublicId, ct);
        if (user is null)
            return new UpdateExampleUserNotFoundFailure("Usuario no encontrado.");

        // actualizar...
        return new UpdateExampleUserSuccess(new ExampleUserDto(user));
    }
}
```

---

## Roles y Claims en el Token JWT

### Emitir claims al generar el token

```csharp
// Infrastructure/Services/JwtTokenService.cs
public string GenerateToken(Guid publicId, string email, IEnumerable<string> roles)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, publicId.ToString()),
        new(ClaimTypes.Email, email),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    };

    // Agregar un claim por cada rol
    claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

    var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer:             _settings.Issuer,
        audience:           _settings.Audience,
        claims:             claims,
        expires:            DateTime.UtcNow.AddMinutes(_settings.ExpiresInMinutes),
        signingCredentials: creds);

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### Verificar roles en Application (sin [Authorize])

```csharp
// En el Handler — verificación explícita sin depender de atributos
if (!_currentUser.IsInRole("admin") && !_currentUser.IsInRole("manager"))
    return new DeleteExampleUserUnauthorizedFailure("Se requiere rol admin o manager.");

// En el Controller — verificación declarativa (más simple para endpoints completos)
[Authorize(Roles = "admin")]
[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { ... }
```

### Claim personalizado

```csharp
// Al generar el token:
claims.Add(new Claim("tenant_id", tenantId.ToString()));
claims.Add(new Claim("plan", "premium"));

// En CurrentUserService:
public Guid? TenantId => Guid.TryParse(GetClaim("tenant_id"), out var id) ? id : null;
public string? Plan   => GetClaim("plan");
```

---

## Audit Trail — CreatedBy / UpdatedBy

Registrar automáticamente qué usuario creó o modificó cada entidad.

### Interfaz de auditoría

```csharp
// Domain/Shared/IAuditable.cs
public interface IAuditable
{
    Guid?    CreatedBy    { get; }
    Guid?    UpdatedBy    { get; }
    DateTime CreatedAtUtc { get; }
    DateTime UpdatedAtUtc { get; }
}
```

### En la entidad

```csharp
public sealed class ExampleUser : IAuditable
{
    public int      Id           { get; init; }
    public Guid     PublicId     { get; init; }
    public string   FullName     { get; private set; } = string.Empty;
    public string   Email        { get; private set; } = string.Empty;
    public bool     IsActive     { get; private set; }

    // Audit fields
    public Guid?    CreatedBy    { get; private set; }
    public Guid?    UpdatedBy    { get; private set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; private set; }
}
```

### SQL con audit trail

```csharp
// INSERT con audit
public Task InsertAsync(ExampleUser user, CancellationToken ct = default) =>
    _db.ExecuteAsync(
        """
        INSERT INTO dbo.ExampleUsers
            (PublicId, FullName, Email, IsActive, CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc)
        VALUES
            (@PublicId, @FullName, @Email, @IsActive, @CreatedBy,
             timezone('utc', now()), @CreatedBy, timezone('utc', now()));
        """,
        new
        {
            user.PublicId, user.FullName, user.Email,
            user.IsActive, user.CreatedBy
        },
        cancellationToken: ct);

// UPDATE con audit
public Task UpdateAsync(ExampleUser user, CancellationToken ct = default) =>
    _db.ExecuteAsync(
        """
        UPDATE dbo.ExampleUsers
        SET FullName     = @FullName,
            Email        = @Email,
            UpdatedBy    = @UpdatedBy,
            UpdatedAtUtc = timezone('utc', now())
        WHERE PublicId = @PublicId;
        """,
        new { user.PublicId, user.FullName, user.Email, user.UpdatedBy },
        cancellationToken: ct);
```

### Handler rellenando el audit automáticamente

```csharp
public async Task<InsertExampleUserResponse> Handle(
    InsertExampleUserRequest request, CancellationToken ct)
{
    var user = new ExampleUser
    {
        PublicId     = Guid.NewGuid(),
        FullName     = request.FullName,
        Email        = request.Email,
        IsActive     = true,
        CreatedBy    = _currentUser.UserId,   // ← quién lo creó
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    await _repo.InsertAsync(user, ct);
    return new InsertExampleUserSuccess(new ExampleUserDto(user));
}
```

---

## HttpClient Tipado + Polly

Para llamar a servicios externos con resiliencia integrada.

### Definir el cliente tipado

```csharp
// Application/Abstractions/IPaymentService.cs  (interfaz en Application)
public interface IPaymentService
{
    Task<PaymentResult> ValidateAsync(string cardToken, decimal amount, CancellationToken ct);
    Task<PaymentResult> ChargeAsync(string cardToken, decimal amount, CancellationToken ct);
}

// Infrastructure/ExternalServices/PaymentService.cs  (implementación en Infrastructure)
public sealed class PaymentService : IPaymentService
{
    private readonly HttpClient _http;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(HttpClient http, ILogger<PaymentService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<PaymentResult> ValidateAsync(
        string cardToken, decimal amount, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("/api/payments/validate",
            new { cardToken, amount }, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Payment validation failed: {Status}", response.StatusCode);
            return PaymentResult.Failed("Validación de pago fallida.");
        }

        return await response.Content.ReadFromJsonAsync<PaymentResult>(ct)
               ?? PaymentResult.Failed("Respuesta vacía del servicio de pagos.");
    }

    public async Task<PaymentResult> ChargeAsync(
        string cardToken, decimal amount, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("/api/payments/charge",
            new { cardToken, amount }, ct);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentResult>(ct)!;
    }
}
```

### Registro con Polly en DI

```csharp
// Host/Extensions/HttpClientsExtensions.cs
public static IServiceCollection AddExternalHttpClients(
    this IServiceCollection services, IConfiguration configuration)
{
    var jitterer = new Random();

    services.AddHttpClient<IPaymentService, PaymentService>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["ExternalServices:PaymentUrl"]
                ?? throw new InvalidOperationException("ExternalServices:PaymentUrl no configurado."));
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("X-Api-Key",
                configuration["ExternalServices:PaymentApiKey"]);
        })
        // Retry: 3 intentos con backoff exponencial + jitter
        .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt =>
                TimeSpan.FromSeconds(Math.Pow(2, attempt))
                + TimeSpan.FromMilliseconds(jitterer.Next(0, 500))))
        // Circuit Breaker: abre si 5 fallos seguidos, espera 30s
        .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30)));

    return services;
}

// Host/Program.cs
builder.Services.AddExternalHttpClients(builder.Configuration);
```

### Registrar IPaymentService en Infrastructure

```csharp
// Infrastructure/ServiceCollectionEx.cs
// AddHttpClient ya registra IPaymentService como Scoped automáticamente
// Solo necesitas registrar la interfaz si NO usas AddHttpClient tipado:
// services.AddScoped<IPaymentService, PaymentService>();
```

### Uso en un Handler

```csharp
public sealed class ProcessPaymentHandler
    : IRequestHandler<ProcessPaymentRequest, ProcessPaymentResponse>
{
    private readonly IPaymentService _payments;
    private readonly IOrderRepository _orders;

    public async Task<ProcessPaymentResponse> Handle(
        ProcessPaymentRequest request, CancellationToken ct)
    {
        var validation = await _payments.ValidateAsync(request.CardToken, request.Amount, ct);
        if (!validation.IsSuccess)
            return new ProcessPaymentValidationFailure(validation.ErrorMessage);

        var result = await _payments.ChargeAsync(request.CardToken, request.Amount, ct);
        if (!result.IsSuccess)
            return new ProcessPaymentFailure(result.ErrorMessage);

        await _orders.MarkAsPaidAsync(request.OrderId, result.TransactionId, ct);
        return new ProcessPaymentSuccess(result.TransactionId);
    }
}
```
