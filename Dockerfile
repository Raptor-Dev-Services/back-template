# Imagen de produccion de la API (.NET 10). Multi-stage: se compila con el SDK y se ejecuta sobre aspnet
# CHISELED (sin shell, sin gestor de paquetes, usuario no-root). La construye el CI y se publica en GHCR; el
# servidor solo la jala, nunca compila.
#
# La imagen final lleva tres cosas: la API, la sonda de salud y el ESQUEMA de su version (migrations.sql + los
# scripts de sql/). Asi el servidor, que solo tiene Docker, migra la base a la version EXACTA que despliega.

# ---- build ----------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero lo que decide el restore: esa capa se reutiliza mientras no cambie ninguna dependencia.
COPY global.json Directory.Build.props Directory.Packages.props dotnet-tools.json ./
COPY Common/ Common/
COPY src/ src/
RUN dotnet restore src/Host/Host.Api/Host.Api.csproj \
 && dotnet restore src/Host/HealthProbe/HealthProbe.csproj \
 && dotnet tool restore

RUN dotnet publish src/Host/Host.Api/Host.Api.csproj -c Release --no-restore -o /app/api /p:UseAppHost=false \
 && dotnet publish src/Host/HealthProbe/HealthProbe.csproj -c Release --no-restore -o /app/probe /p:UseAppHost=false

# Script IDEMPOTENTE de EF: el mismo DDL que `dotnet ef database update`, en SQL plano que corre con solo un
# psql. La version de dotnet-ef la fija dotnet-tools.json (la misma que los paquetes de EF). EF lo escribe con
# BOM y psql lo interpretaria como parte de la primera sentencia: se quita.
RUN mkdir -p /app/migrations \
 && dotnet ef migrations script --idempotent --configuration Release \
      -p src/Shared/Shared.Infrastructure -s src/Host/Host.Api -o /app/migrations/migrations.sql \
 && sed -i '1s/^\xEF\xBB\xBF//' /app/migrations/migrations.sql \
 && cp src/Shared/Shared.Infrastructure/Persistence/Sql/*.sql /app/migrations/

# ---- final ----------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/api ./
COPY --from=build /app/probe ./probe/
# Sin shell no hay `cat`: el deploy extrae estos archivos con `docker create` + `docker cp`.
COPY --from=build /app/migrations ./migrations/

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app

# Readiness (Postgres + almacenamiento), no liveness: un contenedor que arranca pero no alcanza sus
# dependencias queda "unhealthy" a la vista. Docker no lo reinicia por eso; el deploy si espera a que este sano.
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD ["dotnet", "/app/probe/HealthProbe.dll"]

ENTRYPOINT ["dotnet", "Host.Api.dll"]
