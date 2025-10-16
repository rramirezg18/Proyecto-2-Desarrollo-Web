# PROYECTO 3 – DESARROLLO WEB
## 🏀 MARCADOR DE BALONCESTO

**Integrantes**
- Roberto Antonio Ramírez Gómez — 7690-22-12700
- Jean Klaus Castañeda Santos — 7690-22-892
- Jonathan Joel Chan Cuellar — 7690-22-1805

---

# Manual Técnico – Backend (Tablero de Baloncesto)

**Versión:** 1.1.0  
**Fecha:** 2025-10-15  
**Stack:** ASP.NET Core 8 · EF Core · SQL Server 2022 · JWT · SignalR · Docker · FastAPI (report-service) · Redis · Nginx

---

## Tabla de contenidos
1. [Introducción](#1-introducción)  
2. [Alcance](#2-alcance-resumen)  
3. [Arquitectura general](#3-arquitectura-general)  
4. [Estructura del backend](#4-estructura-del-backend)  
5. [Configuración y variables de entorno](#5-configuración-y-variables-de-entorno)  
6. [Seguridad (AuthN/AuthZ)](#6-seguridad-authnauthz)  
7. [Base de datos (migraciones/seed)](#7-base-de-datos)  
8. [Programcs y middleware](#8-programcs-y-middleware)  
9. [Endpoints (resumen)](#9-endpoints-resumen)  
10. [Paginación y filtros](#10-contrato-de-paginación-y-filtros)  
11. [SignalR – Contratos](#11-signalr--contratos-de-mensajes)  
12. [Manejo de errores](#12-manejo-de-errores-problem-details)  
13. [Validaciones (reglas clave)](#13-validaciones-reglas-clave)  
14. [Logs y observabilidad](#14-logs-y-observabilidad)  
15. [Health & readiness](#15-health--readiness)  
16. [Ejecución local](#16-ejecución-local-sin-docker)  
17. [Docker & despliegue](#17-docker--despliegue-compose--nginx--websockets)  
18. [Pruebas (Postman & curl)](#18-pruebas-postman--curl)  
19. [Troubleshooting](#19-troubleshooting)  
20. [Respaldo y restauración de BD](#20-respaldo-y-restauración-de-bd)  
21. [Versionado y licencia](#21-versionado-y-licencia)  
22. [Anexos](#22-anexos)

---

## 1) Introducción
El backend está desarrollado con ASP.NET Core 8 y expone una API RESTful para gestionar equipos, jugadores, partidos y resultados en tiempo real. Utiliza SQL Server 2022 (EF Core) y SignalR para comunicación en vivo. El despliegue se facilita con Docker Compose y Nginx (reverse proxy).

---

## 2) Alcance (resumen)
- Gestión de equipos, jugadores, partidos y tabla de posiciones.  
- **Autenticación/Autorización** por JWT y roles
- **Actualización en vivo** (anotaciones, faltas) vía SignalR.
- **Reportería PDF** desde backend vía **report-service** (expuesto como `/api/reports/*`).

---

## 3) Arquitectura general
- **Tipo:** Monolito API (.NET) + microservicio de reportería (FastAPI)  
- **Patrones:**  
  - **MVC** (controladores)  
  - **Repository Pattern** (acceso a datos)  
  - **Service Layer** (lógica de negocio)  
  - **SignalR** (eventos en tiempo real)  
- **Stack clave:** ASP.NET Core · EF Core · SQL Server · JWT · SignalR · Docker

### Capas y componentes
- **Controllers** → Endpoints REST (`/api/*`)  
- **Services** → Lógica de negocio (`AuthService`, `RoleService`, `MenuService`, etc.)  
- **Repositories** → Acceso a datos (interfaces + implementaciones)  
- **Infrastructure/Data** → `AppDbContext` (Fluent API), Seeds, Migrations  
- **Hubs** → `ScoreHub` (suscripción por `matchId`)  

```
Cliente (Angular)
   │  REST + SignalR
   ▼
ASP.NET Core API ─ Services ─ Repositories ─ EF Core ─ SQL Server
                 └─ SignalR Hub (grupos por partido)
```


```
Cliente (Angular/SPA)
   │
   ├─ /api/*  ───────────────►  ASP.NET Core API (JWT, REST, SignalR)
   │
   ├─ /api/reports/*  ───────►  FastAPI report-service (PDF, Redis)
   │                            ▲
   │                            └── BASE_API = http://api:8080 (consulta datos)
   │
   └─ /hubs/score (WS)  ─────►  SignalR Hub en la API (.NET)
```



---

## 4) Estructura del backend
```
back/Scoreboard/
 ├─ Controllers/            # Auth, Teams, Players, Matches, Standings, Roles, Menu
 ├─ Services/               # AuthService, RoleService, MenuService (+ Interfaces)
 ├─ Repositories/           # Interfaces + implementaciones
 ├─ Models/Entities/        # Team, Player, Match, Foul, ScoreEvent, User, Role, Menu, etc.
 ├─ Data/                   # AppDbContext (Fluent API), Seeds/Migrations
 ├─ Hubs/                   # ScoreHub (SignalR)
 ├─ Program.cs              # DI, CORS, AuthN/AuthZ, rutas, hubs, etc.
 ├─ appsettings*.json       # ConnectionStrings, Jwt, Cors, Logging
 └─ Dockerfile              # Imagen de la API
/report-service/          # FastAPI (PDF) + Redis client
/front/nginx.conf         # Reverse proxy a /api/*, /api/reports/* y WS

```

---

## 5) Configuración y variables de entorno
> En producción, NO dejar secretos en `appsettings.json`. Usar variables de entorno (en .NET se mapean con `__`).

### 5.1 Tabla de variables
| Variable | Ejemplo | Descripción |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Ambiente de ejecución |
| `ConnectionStrings__DefaultConnection` | `Server=db;Database=scoreboard;User Id=sa;Password=...;TrustServerCertificate=True` | Cadena SQL Server |
| `Jwt__Key` | `super-secreto-256bits` | Clave simétrica HMAC |
| `Jwt__Issuer` | `scoreboard.api` | Emisor |
| `Jwt__Audience` | `scoreboard.web` | Audiencia |
| `Jwt__ExpiresInMinutes` | `120` | Expiración del token |
| `Cors__AllowedOrigins__0` | `http://localhost:4200` | Origen permitido (índice 0) |
| `Cors__AllowedOrigins__1` | `https://proyectosdw.lat` | Origen permitido (índice 1) |

### 5.2 Ejemplo `.env`

### 5.3 report-service (FastAPI) — `.env`
| Variable | Ejemplo | Descripción |
|---|---|---|
| `AUTH_SECRET` | `pon_un_secreto_fuerte` | Clave HS256 para **JWT interno RS** |
| `BASE_API` | `http://api:8080` | URL interna de la API .NET |
| `UPSTREAM_TOKEN` | *(opcional)* | Bearer hacia API si se exige |
| `REDIS_URL` | `redis://redis:6379/0` | Conexión a Redis |
| `CACHE_TTL_SECONDS` | `300` | TTL de caché |
| `SERVICE_PORT` | `8080` | Puerto del servicio de reportes |

```env
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Server=db;Database=scoreboard;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=False;TrustServerCertificate=True

Jwt__Key=CHANGEME-32bytes-min
Jwt__Issuer=scoreboard.api
Jwt__Audience=scoreboard.web
Jwt__ExpiresInMinutes=120

Cors__AllowedOrigins__0=http://localhost:4200
Cors__AllowedOrigins__1=https://proyectosdw.lat
```

---

## 6) Seguridad (AuthN/AuthZ)
**Autorización interna (report-service):** El `report-service` valida un **Bearer interno** (JWT HS256) con claim `role=admin`, que puede ser **inyectado por Nginx** al enrutar `/api/reports/*`. La clave de firma debe coincidir con `AUTH_SECRET`.
- **JWT** con HMAC-SHA256; `AuthService` emite tokens con claims (`sub`, `name`, `role`).  
- **Roles**: `admin`, `user`.  
- **Hash de contraseñas**: PBKDF2/BCrypt (recomendado), nunca guardar texto plano.  
- **Políticas** de autorización en endpoints de escritura (crear/editar/borrar).

Cabecera:
```
Authorization: Bearer <JWT>
```

---

## 7) Base de datos
- **Motor:** SQL Server 2022 (Docker)  
- **ORM:** EF Core  
- **Tablas principales:** `Teams`, `Players`, `Matches`, `ScoreEvents`, `Fouls`, `TeamWins`, `Users`, `Roles`, `Menus`, `RoleMenus`.

### 7.1 Migraciones/seed
```bash
# crear migración
dotnet ef migrations add Init
# aplicar
dotnet ef database update
```
> (Opcional) Seeds iniciales de `Roles`/`Users`/`Teams` para demo.

---

## 8) Program.cs y middleware
- **Swagger** habilitado (solo Dev o protegido).  
- **CORS**: permitir orígenes definidos en `Cors:AllowedOrigins`.  
- **DbContext** con SQL Server e inyección de dependencias.  
- **AuthN/AuthZ** con JWT.  
- **SignalR** mapeado:
```csharp
app.MapHub<ScoreHub>("/hubs/score");
```

---

## 9) Endpoints (resumen)
> Los de escritura requieren JWT y rol autorizado.

**Auth (`/api/auth`)**
- `POST /login` → `{ username, password }` → `{ token, expires, role, userId }`
- `POST /register` *(si está habilitado)*

**Teams (`/api/teams`)**
- `GET /` · `GET /{id}` · `POST /` · `PUT /{id}` · `DELETE /{id}`

**Players (`/api/players`)**
- `GET /` · `GET /{id}` · `POST /` · `PUT /{id}` · `DELETE /{id}`

**Matches (`/api/matches`)**
- `GET /` (paginación/filtros) · `POST /` (programar)  
- `POST /{id}/suspend` · `POST /{id}/cancel` *(si aplica)*

**Standings (`/api/standings`)**
- `GET /` (tabla de posiciones)

**Roles (`/api/role`) y Menú (`/api/menu`)**


**Reportes (PDF) — expuestos como** `/api/reports/*`:
- `GET /api/reports/teams.pdf`
- `GET /api/reports/team/{id}/players.pdf`
- `GET /api/reports/matches/history.pdf?from=YYYY-MM-DD&to=YYYY-MM-DD`
- `GET /api/reports/matches/{id}/roster.pdf`
- `GET /api/reports/standings.pdf`

- Roles: CRUD  
- Menú: `GET /` · `GET /{roleId}` · `POST /role/{roleId}` (asignar) · `GET /mine`

---

## 10) Contrato de paginación y filtros
Usar query parametros estándar:
```
GET /api/players?page=1&pageSize=20&sort=name:asc&teamId=12
```
**Respuesta paginada:**
```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "total": 133,
  "hasNext": true
}
```

---

## 11) SignalR – Contratos de mensajes
**Endpoint del hub:** `/hubs/score`  
**Grupos:** uno por `matchId` (`Groups.AddToGroupAsync(connectionId, $"match:{matchId}")`)

**Eventos → cliente**
```json
// scoreUpdated
{ "type":"scoreUpdated", "matchId":25, "teamId":3, "points":3, "home":55, "away":53, "by":"playerId" }

// foulCommitted
{ "type":"foulCommitted", "matchId":25, "teamId":3, "playerId":18, "foulType":"personal", "count":4 }
```

**Cliente JS (ejemplo):**
```js
import * as signalR from "@microsoft/signalr";

const conn = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/score")
  .withAutomaticReconnect()
  .build();

await conn.start();
await conn.invoke("JoinMatch", 25);

conn.on("scoreUpdated", m => console.log(m));
conn.on("foulCommitted", m => console.log(m));
```

---

## 12) Manejo de errores (Problem Details)
Todas las respuestas de error usan RFC 7807 (`application/problem+json`):
```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "El equipo ya existe.",
  "instance": "/api/teams"
}
```

---

## 13) Validaciones (reglas clave)
- Evitar equipos duplicados.  
- No registrar eventos en partidos finalizados.  
- Validar existencia de equipos al crear partido.  
- Validar puntos en `ScoreEvent` (1, 2 o 3).  
- La fecha del partido nopuede ser pasada.

**Códigos HTTP**: `200`, `201`, `400`, `401`, `403`, `404`, `409`, `500`.

---

## 14) Logs y observabilidad
- Logging con `Serilog`.  
- Correlación con `X-Correlation-Id` (si aplica).  
- Métricas básicas: solicitudes/seg, errores, latencia.

**appsettings.json (snippet)**
```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console" ],
    "MinimumLevel": "Information",
    "WriteTo": [ { "Name": "Console" } ]
  }
}
```

---

## 15) Health & readiness
- `GET /api/health` → estado general (200 OK)  
- `GET /api/health/db` → verificación de conexión a BD
- `GET /api/reports/health` → health del **report-service**

---

## 16) Ejecución local (sin Docker)
1) Requisitos: .NET SDK 8+, SQL Server 2022  
2) Configurar `DefaultConnection` en `appsettings.json`  
3) Restaurar/compilar:
```bash
cd back/Scoreboard
dotnet restore
dotnet build
```
4) Migraciones/BD :
```bash
dotnet ef database update
```
5) Ejecutar:
```bash
dotnet run
```
6) La API expone `http://localhost:8080` (o según `launchSettings.json`).

---

## 17) Docker & despliegue (Compose + Nginx + WebSockets)

**Dockerfile (API) – ejemplo**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet","Scoreboard.dll"]
```

**docker-compose.yml (snippet)**
```yaml
services:
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong!Passw0rd
    ports: ["1433:1433"]
    healthcheck:
      test: ["CMD-SHELL","/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $$SA_PASSWORD -Q 'select 1' -No"]
      interval: 10s
      timeout: 3s
      retries: 10

  api:
    build: ./back/Scoreboard
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Server=db;Database=scoreboard;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=False;TrustServerCertificate=True
      - Jwt__Key=CHANGEME-32bytes-min
      - Jwt__Issuer=scoreboard.api
      - Jwt__Audience=scoreboard.web
      - Jwt__ExpiresInMinutes=120
      - Cors__AllowedOrigins__0=http://localhost:4200
    depends_on:
      db:
        condition: service_healthy
    ports: ["8080:8080"]
```

**Nginx (habilitar WebSockets)**
```nginx
# /etc/nginx/sites-available/proyectosdw.conf
server {
  listen 80;
  server_name proyectosdw.lat;

  location / {
    proxy_pass         http://api:8080;
    proxy_set_header   Host $host;
    proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_http_version 1.1;
  }

  # SignalR (WebSockets)
  location /hubs/score {
    proxy_pass         http://api:8080/hubs/score;
    proxy_set_header   Host $host;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection "Upgrade";
    proxy_read_timeout 600s;
  }
}
```

---

## 18) Pruebas (Postman & curl)

**Login → token**
```bash
curl -s -X POST http://localhost:8080/api/auth/login  -H "Content-Type: application/json"  -d '{"username":"admin","password":"admin"}'
```

**Con token**
```bash
curl -H "Authorization: Bearer <JWT>" http://localhost:8080/api/teams
```

Entregar colección Postman** (`/docs/postman/scoreboard.postman_collection.json`) + environment.

---

## 19) Troubleshooting

- **502 Bad Gateway en /api/reports/** → verificar `Authorization` inyectado por Nginx, coincidencia de `AUTH_SECRET` y conectividad `BASE_API`.- **401/403 en /api/reports/** → asegurar claim `role=admin` en el JWT interno.
- **401/403** → Token ausente/expirado o rol insuficiente.  
- **CORS** → Agregar dominio permitido y reiniciar.  
- **SQL Server** → Ver credenciales/puerto; revisar `healthcheck`.  
- **SignalR** → Verificar **Upgrade/Connection** en Nginx; confirmar URL del hub y `matchId`.

---

## 20) Respaldo y restauración de BD
```bash
# dentro del contenedor db
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong!Passw0rd'   -Q "BACKUP DATABASE scoreboard TO DISK='/var/opt/mssql/backup/scoreboard.bak'"
```
> Montar volumen `backup/` para extraer el `.bak`.

---

## 21) Versionado y licencia
- **SemVer**: `MAJOR.MINOR.PATCH` (ej. `1.0.0`).  
- Licencia del proyecto (si aplica).

---

## 22) Anexos
**Matriz roles-permisos (resumen):**
| Recurso   | GET        | POST  | PUT   | DELETE |
|-----------|------------|-------|-------|--------|
| Teams     | user/admin | admin | admin | admin  |
| Players   | user/admin | admin | admin | admin  |
| Matches   | user/admin | admin | admin | admin  |
| Standings | user/admin | –     | –     | –      |
| Reports   | admin      | –     | –     | –      |

---



**Nginx (ruteo /api/reports y WebSockets)**
```nginx
server {
  listen 80;
  server_name proyectosdw.lat;

  # Reportes hacia FastAPI
  location /api/reports/ {
    proxy_pass http://report-service:8080/;
    proxy_http_version 1.1;
    # (Opcional) Inyectar Bearer RS firmado con AUTH_SECRET y role=admin
    proxy_set_header Authorization "Bearer <JWT_RS_INTERNO>";
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
