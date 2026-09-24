# Configuración y entornos

Cómo funciona el sistema de configuración, qué va en cada archivo, y cómo se manejan los secretos.

---

## Los archivos de configuración

```
Host.Api/
├── appsettings.json              ← base — valores por defecto para todos los entornos
├── appsettings.Local.json        ← máquina del dev — sobreescribe la base (no se versiona)
├── appsettings.Development.json  ← entorno Development (se versiona)
├── appsettings.Staging.json      ← entorno Staging (se versiona)
└── appsettings.Production.json   ← entorno Production (se versiona)
```

---

## El orden de carga (el último gana)

```
1. appsettings.json                 ← siempre se carga
2. appsettings.{Entorno}.json       ← sobreescribe la base según ASPNETCORE_ENVIRONMENT
3. appsettings.Local.json           ← sobreescribe todo (si existe)
4. Variables de entorno del OS      ← sobreescribe todo
5. .env (vía docker compose)        ← inyectado como variables de entorno en el contenedor
```

Si la misma clave aparece en varios archivos, **el valor más abajo en la lista gana**. Las variables de entorno del OS siempre tienen prioridad sobre los archivos JSON.

---

## Qué contiene cada archivo

### `appsettings.json` — valores base

El archivo base tiene valores seguros para arrancar pero que deben sobreescribirse en cualquier entorno real. Está versionado en git.

```json
{
  "AllowedHosts": "*",

  "ConnectionStrings": {
    "MainDbConnection": "Host=localhost;Port=5432;Database=back_template;Username=postgres;Password=postgres"
  },

  "Jwt": {
    "Key": "CHANGE_ME_TO_A_SECURE_SECRET_KEY_AT_LEAST_32_CHARS",
    "Issuer": "back-template",
    "Audience": "back-template-clients",
    "ExpirationMinutes": 60,
    "RefreshTokenExpiryDays": 30
  },

  "CustomLogging": {
    "Project": "back-template",
    "Application": "back-template-api",
    "Version": "1.0.0",
    "SeqUri": "",
    "LogEventLevel": "Warning",
    "IncludeSqlText": false
  },

  "Observability": {
    "ServiceName": "back-template-api",
    "ServiceVersion": "1.0.0",
    "OtlpEndpoint": ""
  },

  "Swagger": {
    "Enabled": false
  }
}
```

`Jwt:Key` tiene un placeholder deliberadamente débil. Cualquier deploy real debe sobreescribirlo via variable de entorno.

---

### `appsettings.Local.json` — máquina del dev

Solo contiene lo que es diferente respecto al archivo base. Se fusiona con la base — no hace falta repetir todas las claves.

```json
{
  "ConnectionStrings": {
    "MainDbConnection": "Host=localhost;Port=5432;Database=back_template_dev;Username=postgres;Password=postgres;SSL Mode=Disable"
  },

  "Jwt": {
    "ExpirationMinutes": 480
  },

  "CustomLogging": {
    "LogEventLevel": "Warning",
    "SeqUri": "http://localhost:5341",
    "IncludeSqlText": true
  },

  "Observability": {
    "ServiceName": "back-template-local",
    "OtlpEndpoint": "http://localhost:4317"
  },

  "Swagger": {
    "Enabled": true
  }
}
```

**`IncludeSqlText: true`** activa el SQL completo en los logs de Serilog — solo útil durante desarrollo. Nunca en producción.

**Este archivo no se versiona** (`appsettings.Local.json` está en `.gitignore`). Cada dev tiene el suyo. Si no existe, la app arranca igual usando los valores de `appsettings.json`.

---

### `appsettings.Development.json` y `appsettings.Staging.json`

Para deploy en servidores de desarrollo/staging. Generalmente solo activan Swagger y apuntan a la instancia de Seq del ambiente.

```json
{
  "Swagger": {
    "Enabled": true
  }
}
```

---

### `appsettings.Production.json`

Mínimo posible. Los secretos vienen de variables de entorno, no de este archivo.

```json
{
  "CustomLogging": {
    "LogEventLevel": "Warning",
    "IncludeSqlText": false
  },

  "Swagger": {
    "Enabled": false
  }
}
```

---

## Cómo se selecciona el entorno

ASP.NET Core lee la variable de entorno `ASPNETCORE_ENVIRONMENT` para saber qué `appsettings.{Entorno}.json` cargar.

```bash
# En desarrollo local (launchSettings.json ya lo configura)
ASPNETCORE_ENVIRONMENT=Local

# En el servidor o Docker
ASPNETCORE_ENVIRONMENT=Production
```

Los valores posibles en este proyecto:

| `ASPNETCORE_ENVIRONMENT` | `appsettings.*.json` cargado | Swagger |
|--------------------------|------------------------------|---------|
| `Local` | `appsettings.Local.json` | ✓ |
| `Development` | `appsettings.Development.json` | ✓ |
| `Staging` | `appsettings.Staging.json` | ✓ |
| `Production` | `appsettings.Production.json` | ✗ |

---

## Variables de entorno — los secretos reales

Las variables de entorno sobreescriben cualquier valor de los archivos JSON. En producción, los secretos nunca van en archivos — van como variables de entorno.

### Formato de clave

ASP.NET Core mapea secciones anidadas usando `:` (Linux/Mac) o `__` (Windows/Docker). Ambos funcionan.

```bash
# Equivalente a:  { "Jwt": { "Key": "mi-clave-secreta" } }
Jwt__Key=<JWT_KEY_ALEATORIA_DE_32_BYTES_O_MAS>

# Equivalente a:  { "ConnectionStrings": { "MainDbConnection": "..." } }
ConnectionStrings__MainDbConnection=Host=192.168.1.100;Port=5432;...
```

### Variables obligatorias en producción

```env
ASPNETCORE_ENVIRONMENT=Production
Jwt__Key=<clave JWT — mínimo 32 caracteres aleatorios>
ConnectionStrings__MainDbConnection=Host=...;Database=...;Username=...;Password=...
```

### Variables opcionales en producción

```env
CustomLogging__SeqUri=http://seq:5341
Observability__OtlpEndpoint=http://jaeger:4317
```

---

## Docker y archivos .env

Cuando la app corre en Docker Compose, las variables de entorno se pasan al contenedor vía el archivo `.env` en la raíz del repo.

```bash
# .env.example — versionar este archivo (sin valores reales)
POSTGRES_PASSWORD=
JWT_KEY=
SEQ_URI=http://seq:5341
OTLP_ENDPOINT=http://jaeger:4317

# .env — NO versionar (está en .gitignore)
POSTGRES_PASSWORD=mi-password-real
JWT_KEY=mi-clave-jwt-de-32-caracteres-como-minimo
```

En `compose.yaml` o `compose-dev.yaml`:

```yaml
services:
  api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Jwt__Key=${JWT_KEY}
      - ConnectionStrings__MainDbConnection=Host=db;Port=5432;Database=back_template;Username=postgres;Password=${POSTGRES_PASSWORD}
      - CustomLogging__SeqUri=${SEQ_URI}
```

---

## Leer configuración en código

La configuración está disponible en cualquier `ServiceCollectionEx` vía `IConfiguration`:

```csharp
// En Presentation o Infrastructure ServiceCollectionEx que necesite config
public static IServiceCollection AddAuthenticationInfrastructureServices(
    this IServiceCollection services, IConfiguration configuration)
{
    var jwtKey = configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key no configurado.");
    // ...
}
```

Para claves complejas, usar Options con validación:

```csharp
// Common.Options
services.AddValidatedOptions<JwtOptions>("Jwt");

public sealed class JwtOptions
{
    [Required, MinLength(32)]
    public string Key { get; init; } = default!;

    [Required]
    public string Issuer { get; init; } = default!;

    [Required]
    public string Audience { get; init; } = default!;

    public int ExpirationMinutes { get; init; } = 60;
    public int RefreshTokenExpiryDays { get; init; } = 30;
}
```

Si las validaciones fallan, la app no arranca y Serilog loguea exactamente qué campo falta.

---

## Reglas

1. **Nunca poner secretos reales en ningún `appsettings*.json`** — ni la JWT key, ni passwords de BD, ni API keys. Siempre variables de entorno.
2. **`appsettings.Local.json` no se versiona** — cada dev tiene el suyo. Si hace falta un campo nuevo, documentarlo en README o en este archivo.
3. **`appsettings.json` debe poder arrancar en dev sin cambios** — los valores por defecto deben funcionar con `compose-db.yaml` levantado.
4. **Ambiente = lo que dice `ASPNETCORE_ENVIRONMENT`** — no inferir el entorno de ninguna otra forma.
5. **`Jwt:Key` en producción mínimo 32 chars** — `JwtAuthExtensions` lanza excepción si no está configurado. No hay fallback.

---

## Jerarquía visual

```
appsettings.json          → base (siempre se carga)
    + appsettings.Local.json   → sobreescribe si existe (dev local, no versionado)
    + appsettings.{Env}.json   → sobreescribe según ASPNETCORE_ENVIRONMENT
        + Variables de entorno → siempre tienen prioridad (secretos de producción)
```
