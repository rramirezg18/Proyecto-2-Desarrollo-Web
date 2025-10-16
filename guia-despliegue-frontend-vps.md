# Guía de Despliegue en VPS — **Frontend Angular (Tablero de Baloncesto)**

**Versión:** 1.0.0  
**Fecha:** 2025-10-15  

>Preparada para **Ubuntu 22.04/24.04 LTS** sin interfaz gráfica en un VPS  DigitalOcean).  
> Funciona en dos formas diferentes:
> 1) SPA estática con Nginx.  
> 2) SSR (Node/Express) + Nginx.

---

## 0) Requisitos previos
- Usuario con **sudo** (ej.: `melgust`) y acceso SSH por llave.
- DNS apuntando al VPS (ej.: `proyectosdw.lat` o `app.proyectosdw.lat`).
- **Nginx** instalado:
  ```bash
  sudo apt update && sudo apt install -y nginx
  ```
- (Opcional HTTPS) **Certbot**:
  ```bash
  sudo apt install -y certbot python3-certbot-nginx
  ```
- **Node.js 20+** y **npm** (solo para SSR o builds locales):
  ```bash
  sudo apt install -y curl && curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
  sudo apt install -y nodejs
  ```
- **Docker + Docker Compose** (si vas a contenerizar el frontend):
  ```bash
  sudo apt install -y docker.io
  sudo usermod -aG docker $USER
  # cerrar sesión y volver a entrar
  ```

> La app usa rutas relativas (`/api`, `/api/reports` y `/hubs/score`), por lo que **no necesitas recompilar** para cambiar dominios si el proxy está bien configurado.

---

## 1) Estructura esperada en el VPS
```
/opt/scoreboard-frontend/
  ├─ dist/scoreboard/browser/   # build de producción (SPA)
  ├─ dist/scoreboard/server/    # build SSR (opcional)
  ├─ nginx/scoreboard.conf      # virtual host Nginx
  ├─ docker/                    # (opcional) Dockerfile / compose
  └─ logs/                      # logs de Nginx o PM2/systemd (SSR)
```

---

## 2) Opción A — **SPA estática con Nginx** (recomendada)

### A.1 Build de producción (si construyes en el VPS)
```bash
# Clonar repo y entrar a front/scoreboard
cd /opt && sudo git clone <TU_REPO> scoreboard
cd scoreboard/front/scoreboard
sudo npm ci
sudo npm run build -- --configuration production
# Resultado en: dist/scoreboard/browser
```

> Alternativa: construye el `dist/scoreboard/browser/` en tu máquina y súbelo por scp/rsync al VPS.

### A.2 Virtual host Nginx
Guarda como `/etc/nginx/sites-available/scoreboard.conf`:
```nginx
server {
  listen 80;
  server_name proyectosdw.lat;   # cambia al tuyo

  root /opt/scoreboard-frontend/dist/scoreboard/browser;
  index index.html;

  # API principal (.NET)
  location /api/ {
    proxy_pass http://api:8080/;          # ajusta al nombre/puerto del servicio backend
    proxy_http_version 1.1;
  }

  # Reportes (FastAPI)
  location /api/reports/ {
    proxy_pass http://report-service:8080/;
    proxy_http_version 1.1;
    # Si tu backend requiere un Bearer interno para reportes,
    # descomenta y reemplaza por un JWT válido:
    # proxy_set_header Authorization "Bearer <JWT_RS_INTERNO>";
  }

  # SignalR (WebSockets)
  location /hubs/score {
    proxy_pass         http://api:8080/hubs/score;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection "upgrade";
    proxy_set_header   Host $host;
    proxy_read_timeout 600s;
  }

  # SPA fallback
  location / {
    try_files $uri $uri/ /index.html;
  }

  # (Opcional) Cache estáticos
  location ~* \.(?:js|css|png|jpg|jpeg|gif|svg|ico|woff2?)$ {
    expires 7d;
    access_log off;
  }
}
```

Activar y recargar:
```bash
sudo mkdir -p /opt/scoreboard-frontend/dist/scoreboard/browser
# Copia aquí tu build (si no lo hiciste en A.1)

sudo ln -sf /etc/nginx/sites-available/scoreboard.conf /etc/nginx/sites-enabled/scoreboard.conf
sudo nginx -t && sudo systemctl reload nginx
```

### A.3 HTTPS (opcional pero recomendado)
```bash
sudo certbot --nginx -d proyectosdw.lat -m tu-correo@dominio.com --agree-tos --redirect
```

---

## 3) Opción B — **SSR (Node/Express) + Nginx** (opcional)
Esta modalidad renderiza en el servidor y puede mejorar SEO/TTFB.

### B.1 Build SSR
```bash
cd /opt/scoreboard/front/scoreboard
sudo npm ci
sudo npm run build      # genera browser + server
# Artefactos:
#   dist/scoreboard/browser/
#   dist/scoreboard/server/server.mjs
```

### B.2 Servicio Node (systemd o PM2)

**Systemd** — crea `/etc/systemd/system/scoreboard-ssr.service`:
```ini
[Unit]
Description=Scoreboard Frontend SSR
After=network.target

[Service]
Type=simple
User=www-data
WorkingDirectory=/opt/scoreboard/front/scoreboard
Environment=PORT=4000
ExecStart=/usr/bin/node dist/scoreboard/server/server.mjs
Restart=always

[Install]
WantedBy=multi-user.target
```

Habilitar y arrancar:
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now scoreboard-ssr
sudo systemctl status scoreboard-ssr
```

**PM2** (alternativa):
```bash
sudo npm i -g pm2
pm2 start dist/scoreboard/server/server.mjs --name scoreboard-ssr --env production -- 4000
pm2 save
```

### B.3 Nginx (proxy a SSR)
`/etc/nginx/sites-available/scoreboard-ssr.conf`:
```nginx
server {
  listen 80;
  server_name proyectosdw.lat;

  # Frontend SSR (Node/Express)
  location / {
    proxy_pass http://127.0.0.1:4000/;
    proxy_http_version 1.1;
  }

  # API principal (.NET)
  location /api/ {
    proxy_pass http://api:8080/;
    proxy_http_version 1.1;
  }

  # Reportes (FastAPI)
  location /api/reports/ {
    proxy_pass http://report-service:8080/;
    proxy_http_version 1.1;
    # proxy_set_header Authorization "Bearer <JWT_RS_INTERNO>";
  }

  # SignalR (WebSockets)
  location /hubs/score {
    proxy_pass         http://api:8080/hubs/score;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection "upgrade";
    proxy_set_header   Host $host;
    proxy_read_timeout 600s;
  }
}
```
Activar:
```bash
sudo ln -sf /etc/nginx/sites-available/scoreboard-ssr.conf /etc/nginx/sites-enabled/scoreboard.conf
sudo nginx -t && sudo systemctl reload nginx
```

### B.4 HTTPS
```bash
sudo certbot --nginx -d proyectosdw.lat -m tu-correo@dominio.com --agree-tos --redirect
```

---

## 4) Opción C — **Docker** (frontend estático con Nginx)
**Dockerfile** (en `front/scoreboard/`):
```dockerfile
# Build
FROM node:20 AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration production

# Runtime
FROM nginx:alpine
COPY --from=build /app/dist/scoreboard/browser/ /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx","-g","daemon off;"]
```

**nginx.conf** (igual a la sección A.2).

**Compose (fragmento)**
```yaml
services:
  web:
    build: ./front/scoreboard
    ports:
      - "80:80"
    depends_on:
      - api
      - report-service
```



---

## 5) Checklist de verificación
- `curl -I http://proyectosdw.lat` → `200 OK` y `Content-Type: text/html`.
- Abrir `http://proyectosdw.lat` → carga la SPA/SSR.
- **Auth**: login correcto y `Authorization: Bearer` en solicitudes XHR.
- **Reportes**: descarga de `/api/reports/*.pdf` (200 OK). Si sale **401/502**, revisar **JWT interno** y ruteo.
- **Tiempo real**: en DevTools → *Network* → **WS** conectado a `/hubs/score` (101 Switching Protocols).

---

## 6) Problemas comunes
- **502 en /api/reports/** → falta/incorrecto `proxy_set_header Authorization` (si tu backend lo exige) o `report-service` caído.
- **401/403** → token del usuario vencido; verifica interceptor y sesión.
- **WebSockets no conectan** → faltan cabeceras `Upgrade/Connection` en Nginx.
- **404 al refrescar** (SPA) → falta `try_files ... /index.html;`.
- **CORS en desarrollo** → usa `ng serve --proxy-config proxy.conf.json`.

---

## 7) Rollback rápido
- Mantén la versión anterior de `/opt/scoreboard-frontend/dist/...` en un subdirectorio `backup/`.
- Para volver: `sudo rm -rf dist/scoreboard/browser && sudo mv backup/browser dist/scoreboard/ && sudo systemctl reload nginx`.
- En SSR: `sudo systemctl restart scoreboard-ssr`.

---

## 8) Datos útiles
- Logs Nginx: `/var/log/nginx/access.log` y `error.log`.
- Probar configuración: `sudo nginx -t`.
- Abrir puertos: `sudo ufw allow 80,443/tcp`.
- Estado servicios: `systemctl status nginx` (y `scoreboard-ssr` si usas SSR).
