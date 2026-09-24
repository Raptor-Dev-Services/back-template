# Paquetes

Las versiones viven en **un solo lugar**: `Directory.Packages.props` (Central Package Management). Un `.csproj`
declara `<PackageReference Include="X" />` sin version. El submodulo `Common/` queda fuera de CPM.

`Directory.Build.props` fija `net10.0`, `TreatWarningsAsErrors` y **NuGetAudit**: un paquete con vulnerabilidad
conocida es un warning y, por tanto, rompe el build. El SDK lo fija `global.json` (10.0.100, `latestFeature`).

## Produccion

| Paquete | Version | Para que |
|---|---|---|
| Microsoft.EntityFrameworkCore (+ Relational, Design) | 10.0.12 | ORM |
| Npgsql / Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.12 | validacion de JWT |
| Microsoft.IdentityModel.JsonWebTokens | 8.23.0 | emision de JWT (`JsonWebTokenHandler`) |
| BCrypt.Net-Next | 4.2.0 | hash de contrasenas |
| Otp.NET | 1.4.1 | TOTP |
| Minio | 7.0.0 | almacenamiento de objetos S3 |
| Serilog.AspNetCore | 10.0.0 | logging |
| DotNetEnv | 3.2.0 | `.env` en Development |
| Swashbuckle.AspNetCore | 10.2.3 | Swagger |
| Microsoft.Extensions.*.Abstractions | 10.0.12 | DI, configuracion, hosting, logging en capas sin ASP.NET |

## Pruebas

| Paquete | Version |
|---|---|
| Microsoft.NET.Test.Sdk | 18.10.1 |
| xunit / xunit.runner.visualstudio | 2.9.3 / 4.0.0 |
| coverlet.collector | 10.0.0 |
| NSubstitute | 5.3.0 |
| NetArchTest.Rules | 1.3.2 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 |
| Testcontainers.PostgreSql | 4.15.0 |
| SSH.NET | 2026.0.0 (pin de seguridad: Testcontainers arrastra una version con GHSA-q939-rpr3-3284) |

## Herramientas

`dotnet-tools.json` fija `dotnet-ef` a la version de EF (`dotnet tool restore`). El Dockerfile y
`scripts/dev-db.sh` usan esa.

## Reglas

- Solo versiones estables: la puerta `dependencias-estables` (`scripts/check-prerelease-deps.py`) falla ante un
  prerelease o una version flotante que no este declarada en `PRERELEASE-PERMITIDOS.txt` con motivo.
- No agregar otro mediador (MediatR y similares): el de Common es el del patron.
- Subir una version = editar `Directory.Packages.props`, build con `-warnaserror` y pruebas.
