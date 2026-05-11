# ---- Stage 1: restore ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY back-template/back-template.slnx                        back-template/
COPY back-template/Common/                                   back-template/Common/
COPY back-template/Domain/Domain.csproj                      back-template/Domain/
COPY back-template/Application/Application.csproj            back-template/Application/
COPY back-template/Infrastructure/Infrastructure.csproj      back-template/Infrastructure/
COPY back-template/WebApi/WebApi.csproj                      back-template/WebApi/
COPY back-template/Host/Host.csproj                          back-template/Host/

RUN dotnet restore back-template/back-template.slnx

# ---- Stage 2: build ----
FROM restore AS build
COPY back-template/ back-template/
RUN dotnet build back-template/back-template.slnx \
    -c Release \
    --no-restore

# ---- Stage 3: publish ----
FROM build AS publish
RUN dotnet publish back-template/Host/Host.csproj \
    -c Release \
    --no-build \
    -o /app/publish \
    /p:UseAppHost=false

# ---- Stage 4: final (distroless) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Host.dll"]
