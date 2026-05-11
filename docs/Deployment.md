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
# Actualizar
sudo apt update && sudo apt upgrade -y

# Instalar Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER   # agregar tu usuario al grupo docker
newgrp docker                    # aplicar sin cerrar sesión

# Verificar
docker --version
docker compose version
```

### 2. Subir el código y construir la imagen

```bash
# Clonar el repo en el servidor
git clone --recurse-submodules https://github.com/tu-org/back-template.git
cd back-template

# Crear el .env de producción (NUNCA subir este archivo al repo)
cat > .env << 'EOF'
POSTGRES_PASSWORD=<contraseña-segura-min-32-chars>
JWT_KEY=<clave-jwt-min-32-chars-aleatoria>
EOF

# Construir la imagen
docker build -t back-template:latest -f back-template/Dockerfile .

# O usar compose para build + up
docker compose up -d --build
```

### 3. Variables de entorno en producción

```bash
# .env en la raíz del repo (en el servidor, NUNCA en git)
POSTGRES_PASSWORD=R4nd0m_S3cur3_P4ssw0rd!!
JWT_KEY=m1-cl4v3-jwt-sup3r-s3cr3t4-d3-m4s-d3-32-ch4rs!!
POSTGRES_USER=app_user
POSTGRES_DB=mydb

# Permisos restrictivos — solo el dueño puede leer
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
docker compose up -d --build --force-recreate api   # solo reconstruye la API, no la DB

# Rollback rápido a imagen anterior
docker tag back-template:latest back-template:prev
docker compose up -d   # si el nuevo build falla, regresar al anterior

# Backup de la base de datos
docker compose exec postgres pg_dump -U app_user mydb > backup_$(date +%Y%m%d).sql

# Restaurar backup
cat backup_20260511.sql | docker compose exec -T postgres psql -U app_user mydb
```

---

## Nginx como Reverse Proxy

La API escucha en el puerto 8080 dentro de Docker. Nginx maneja SSL y redirige al contenedor.

### Instalar Nginx

```bash
sudo apt install nginx -y
sudo systemctl enable nginx
```

### Configuración del virtual host

```nginx
# /etc/nginx/sites-available/back-template
server {
    listen 80;
    server_name api.tudominio.com;

    # Redirigir todo HTTP a HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.tudominio.com;

    # SSL — Let's Encrypt (ver sección SSL más abajo)
    ssl_certificate     /etc/letsencrypt/live/api.tudominio.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.tudominio.com/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;

    # HSTS
    add_header Strict-Transport-Security "max-age=63072000" always;

    # Security headers
    add_header X-Frame-Options "DENY";
    add_header X-Content-Type-Options "nosniff";
    add_header Referrer-Policy "strict-origin-when-cross-origin";

    # Proxy hacia el contenedor Docker
    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;

        # Timeouts
        proxy_connect_timeout 30s;
        proxy_send_timeout    60s;
        proxy_read_timeout    60s;

        # Para uploads
        client_max_body_size 10M;
    }

    # Health check — sin logs para no saturar
    location /api/health {
        proxy_pass http://127.0.0.1:8080;
        access_log off;
    }
}
```

```bash
# Activar y verificar
sudo ln -s /etc/nginx/sites-available/back-template /etc/nginx/sites-enabled/
sudo nginx -t          # verificar sintaxis
sudo systemctl reload nginx
```

---

## Caddy — alternativa más simple

Caddy maneja SSL automáticamente sin configuración extra.

```caddyfile
# /etc/caddy/Caddyfile
api.tudominio.com {
    reverse_proxy localhost:8080

    header {
        X-Frame-Options "DENY"
        X-Content-Type-Options "nosniff"
        Referrer-Policy "strict-origin-when-cross-origin"
        -Server
    }

    log {
        output file /var/log/caddy/api.log
        format json
    }
}
```

```bash
# Instalar Caddy
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
echo "deb [signed-by=/usr/share/keyrings/caddy-stable-archive-keyring.gpg] https://dl.cloudsmith.io/public/caddy/stable/deb/debian any-version main" | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update && sudo apt install caddy

sudo systemctl enable caddy
sudo systemctl start caddy
```

**Ventaja de Caddy:** HTTPS automático con Let's Encrypt sin configuración adicional.

---

## SSL con Let's Encrypt (para Nginx)

```bash
# Instalar Certbot
sudo apt install certbot python3-certbot-nginx -y

# Obtener certificado
sudo certbot --nginx -d api.tudominio.com

# Renovación automática (certbot crea un cronjob automáticamente)
sudo certbot renew --dry-run   # verificar que funciona

# Ver cuando expira
sudo certbot certificates
```

---

## systemd sin Docker

Para desplegar el binario .NET directamente en el servidor sin Docker.

### Publicar y copiar el binario

```bash
# En el servidor de CI o en local:
dotnet publish back-template/Host/Host.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o /tmp/publish

# Copiar al servidor
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
ExecStart=/usr/bin/dotnet /opt/back-template/Host.dll
Restart=always
RestartSec=5

# Usuario sin privilegios
User=www-data
Group=www-data

# Variables de entorno de producción
EnvironmentFile=/etc/back-template/production.env

# Límites de recursos
LimitNOFILE=65535
MemoryLimit=512M

# Seguridad
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
# Activar y gestionar
sudo systemctl daemon-reload
sudo systemctl enable back-template
sudo systemctl start back-template
sudo systemctl status back-template

# Ver logs
sudo journalctl -u back-template -f
sudo journalctl -u back-template --since "1 hour ago"
```

### systemd vs Docker

| Aspecto | systemd | Docker |
|---------|---------|--------|
| Complejidad | Menor | Mayor |
| Reproducibilidad | Baja (depende del OS del servidor) | Alta (misma imagen en todos lados) |
| Rollback | Manual | `docker tag` + `up` |
| Múltiples servicios | Múltiples units | `compose.yaml` |
| Uso recomendado | Servidor dedicado simple | Cualquier entorno moderno |

---

## Checklist pre-deploy

```
Entorno y configuración:
[ ] ASPNETCORE_ENVIRONMENT=Production
[ ] JWT_KEY definido y tiene ≥32 caracteres
[ ] POSTGRES_PASSWORD definido y seguro
[ ] HTTPS configurado (Nginx/Caddy + certificado)
[ ] app.UseHttpsRedirection() descomentado en Program.cs

Base de datos:
[ ] Backup antes de cada deploy
[ ] Migraciones SQL verificadas (idempotentes)
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
[ ] dotnet build 0 errores
[ ] dotnet test pasa
[ ] docker build --no-cache (imagen limpia)
[ ] Imagen correcta: mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
```
