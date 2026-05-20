# Deployment.md — Despliegue en Producción

---

## Opciones de despliegue

| Opción | Cuándo usar |
|--------|------------|
| **Docker + VPS** | Control total, costo bajo, un servidor |
| **Docker Compose en VPS** | Lo más común — API + DB + servicios en un servidor |
| **systemd sin Docker** | Servidor sin Docker, bare metal |
| **Cloud managed** | Azure App Service, AWS ECS, Railway, Render |

---

## Docker Compose en VPS — el flujo más común

### 1. Preparar el servidor (Ubuntu 24.04 LTS)

```bash
sudo apt update && sudo apt upgrade -y
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
newgrp docker
docker --version && docker compose version
```

### 2. Subir el código y construir la imagen

```bash
git clone --recurse-submodules https://github.com/tu-org/back-template.git
cd back-template

# Crear el .env de producción (NUNCA subir al repo)
cat > .env << 'EOF'
POSTGRES_PASSWORD=<contraseña-segura-min-32-chars>
JWT_KEY=<clave-jwt-min-32-chars-aleatoria>
EOF

docker compose up -d --build
```

### 3. Variables de entorno en producción

```bash
# .env en la raíz del repo (en el servidor, NUNCA en git)
POSTGRES_PASSWORD=R4nd0m_S3cur3_P4ssw0rd!!
JWT_KEY=m1-cl4v3-jwt-sup3r-s3cr3t4-d3-m4s-d3-32-ch4rs!!
POSTGRES_USER=app_user
POSTGRES_DB=mydb
chmod 600 .env
```

```yaml
# compose.yaml — referencias al .env
services:
  api:
    environment:
      Jwt__Key:                               "${JWT_KEY}"
      ConnectionStrings__MainDbConnection:    "Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      ASPNETCORE_ENVIRONMENT:                 "Production"
      ASPNETCORE_HTTP_PORTS:                  "8080"
    restart: unless-stopped
```

### 4. Comandos de operación habitual

```bash
# Ver estado
docker compose ps
docker compose logs -f api

# Actualizar tras nuevo deploy
git pull
docker compose up -d --build --force-recreate api

# Backup de la base de datos
docker compose exec postgres pg_dump -U app_user mydb > backup_$(date +%Y%m%d).sql

# Restaurar backup
cat backup_20260511.sql | docker compose exec -T postgres psql -U app_user mydb
```

---

## Nginx como Reverse Proxy

```bash
sudo apt install nginx -y
sudo systemctl enable nginx
```

```nginx
# /etc/nginx/sites-available/back-template
server {
    listen 80;
    server_name api.tudominio.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.tudominio.com;

    ssl_certificate     /etc/letsencrypt/live/api.tudominio.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.tudominio.com/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;

    add_header Strict-Transport-Security "max-age=63072000" always;
    add_header X-Frame-Options "DENY";
    add_header X-Content-Type-Options "nosniff";

    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }

    location /api/health {
        proxy_pass http://127.0.0.1:8080;
        access_log off;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/back-template /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## Caddy — alternativa más simple

Maneja SSL automáticamente con Let's Encrypt sin configuración extra.

```caddyfile
api.tudominio.com {
    reverse_proxy localhost:8080
    header {
        X-Frame-Options "DENY"
        X-Content-Type-Options "nosniff"
        -Server
    }
}
```

---

## SSL con Let's Encrypt (para Nginx)

```bash
sudo apt install certbot python3-certbot-nginx -y
sudo certbot --nginx -d api.tudominio.com
sudo certbot renew --dry-run   # verificar renovación automática
```

---

## systemd sin Docker

### Publicar el binario

```bash
dotnet publish back-template/Host.Api/Host.Api.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o /tmp/publish

scp -r /tmp/publish/* user@servidor:/opt/back-template/
```

### Crear el servicio systemd

```ini
# /etc/systemd/system/back-template.service
[Unit]
Description=Back Template API
After=network.target postgresql.service

[Service]
Type=notify
WorkingDirectory=/opt/back-template
ExecStart=/usr/bin/dotnet /opt/back-template/Host.Api.dll
Restart=always
RestartSec=5

User=www-data
Group=www-data

EnvironmentFile=/etc/back-template/production.env

LimitNOFILE=65535
MemoryLimit=512M

NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=/opt/back-template/logs

[Install]
WantedBy=multi-user.target
```

```bash
# /etc/back-template/production.env (permisos 600, dueño root)
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_HTTP_PORTS=8080
Jwt__Key=mi-clave-secreta
ConnectionStrings__MainDbConnection=Host=localhost;Port=5432;Database=mydb;...
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable back-template
sudo systemctl start back-template
sudo journalctl -u back-template -f
```

---

## Checklist pre-deploy

```
Entorno y configuración:
[ ] ASPNETCORE_ENVIRONMENT=Production
[ ] JWT_KEY definido y tiene ≥32 caracteres
[ ] POSTGRES_PASSWORD definido y seguro
[ ] HTTPS configurado (Nginx/Caddy + certificado)
[ ] app.UseHttpsRedirection() activo en Host.Api/Program.cs

Base de datos:
[ ] Backup antes de cada deploy
[ ] DatabaseInitializationService verificado — EnsureCreatedAsync() arranca sin errores en staging
[ ] Usuario de DB con permisos mínimos (no superuser)

Seguridad:
[ ] .env no está en el repositorio (.gitignore)
[ ] Security headers configurados
[ ] CORS solo permite orígenes del frontend real
[ ] Rate limiting activo en endpoints de auth

Observabilidad:
[ ] Seq/logging externo configurado con la URL de producción
[ ] Health check responde: GET /api/health
[ ] Jaeger/OTLP endpoint configurado

Build:
[ ] dotnet build Host.Api/Host.Api.csproj — 0 errores
[ ] dotnet test Tests/Tests.csproj — pasa
[ ] docker build --no-cache (imagen limpia)
[ ] Imagen correcta: mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
```
