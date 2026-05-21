# ---- Stage 1: restore ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Copy solution + all .csproj files to maximize layer cache on restore
COPY back-template/back-template.slnx                                                                      back-template/
COPY back-template/Common/                                                                                 back-template/Common/

# Shared infrastructure
COPY back-template/Shared/Database/Shared.Database.csproj                                                 back-template/Shared/Database/
COPY back-template/Shared/Web/Shared.Web.csproj                                                           back-template/Shared/Web/

# Authentication shared module
COPY back-template/Shared/Authentication/Authentication.Contracts/Authentication.Contracts.csproj          back-template/Shared/Authentication/Authentication.Contracts/
COPY back-template/Shared/Authentication/Authentication.Domain/Authentication.Domain.csproj                back-template/Shared/Authentication/Authentication.Domain/
COPY back-template/Shared/Authentication/Authentication.Application/Authentication.Application.csproj      back-template/Shared/Authentication/Authentication.Application/
COPY back-template/Shared/Authentication/Authentication.Infrastructure/Authentication.Infrastructure.csproj back-template/Shared/Authentication/Authentication.Infrastructure/
COPY back-template/Shared/Authentication/Authentication.Presentation/Authentication.Presentation.csproj    back-template/Shared/Authentication/Authentication.Presentation/

# Tenancy module
COPY back-template/Modules/Tenancy/Tenancy.Contracts/Tenancy.Contracts.csproj                             back-template/Modules/Tenancy/Tenancy.Contracts/
COPY back-template/Modules/Tenancy/Tenancy.Domain/Tenancy.Domain.csproj                                   back-template/Modules/Tenancy/Tenancy.Domain/
COPY back-template/Modules/Tenancy/Tenancy.Application/Tenancy.Application.csproj                         back-template/Modules/Tenancy/Tenancy.Application/
COPY back-template/Modules/Tenancy/Tenancy.Infrastructure/Tenancy.Infrastructure.csproj                   back-template/Modules/Tenancy/Tenancy.Infrastructure/
COPY back-template/Modules/Tenancy/Tenancy.Presentation/Tenancy.Presentation.csproj                       back-template/Modules/Tenancy/Tenancy.Presentation/

# Users module
COPY back-template/Modules/Users/Users.Contracts/Users.Contracts.csproj                                   back-template/Modules/Users/Users.Contracts/
COPY back-template/Modules/Users/Users.Domain/Users.Domain.csproj                                         back-template/Modules/Users/Users.Domain/
COPY back-template/Modules/Users/Users.Application/Users.Application.csproj                               back-template/Modules/Users/Users.Application/
COPY back-template/Modules/Users/Users.Infrastructure/Users.Infrastructure.csproj                         back-template/Modules/Users/Users.Infrastructure/
COPY back-template/Modules/Users/Users.Presentation/Users.Presentation.csproj                             back-template/Modules/Users/Users.Presentation/

# Host
COPY back-template/Host.Api/Host.Api.csproj                                                               back-template/Host.Api/

RUN dotnet restore back-template/Host.Api/Host.Api.csproj

# ---- Stage 2: build ----
FROM restore AS build
COPY back-template/ back-template/
RUN dotnet build back-template/Host.Api/Host.Api.csproj \
    -c Release \
    --no-restore

# ---- Stage 3: publish ----
FROM build AS publish
RUN dotnet publish back-template/Host.Api/Host.Api.csproj \
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
ENTRYPOINT ["dotnet", "Host.Api.dll"]
