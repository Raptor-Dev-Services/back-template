# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo lo que decide el restore (props centrales, SDK fijado, .csproj): esa capa se
# reutiliza mientras no cambie ninguna dependencia.
COPY global.json Directory.Build.props Directory.Packages.props back-template.slnx ./
COPY Common/ Common/
COPY src/ src/
RUN dotnet restore src/Host/Host.Api/Host.Api.csproj

RUN dotnet publish src/Host/Host.Api/Host.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

# ---- final (chiseled: sin shell ni gestor de paquetes, usuario no-root) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Host.Api.dll"]
