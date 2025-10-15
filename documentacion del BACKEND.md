# PROYECTO 1 – DESARROLLO WEB
## 🏀 MARCADOR DE BALONCESTO

**Integrantes**
- Roberto Antonio Ramírez Gómez — 7690-22-12700
- Jean Klaus Castañeda Santos — 7690-22-892
- Jonathan Joel Chan Cuellar — 7690-22-1805

---

# Documentación Técnica – Backend (Tablero Basket)

## 1) Introducción
El backend está desarrollado con ASP.NET Core 8 y expone una API RESTful para gestionar equipos, jugadores, partidos y resultados en tiempo real. Utiliza SQL Server 2022 (con EF Core) y SignalR para comunicación en tiempo real. El despliegue se facilita con Docker Compose.

---

## 2) Arquitectura general
- **Tipo:** Monolito modular
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
- **Infrastructure/Data** → `AppDbContext` (Fluent API) y configuración
- **Hubs** → `ScoreHub` (suscripción por `matchId`)

```
Cliente (Angular)
   │  REST + SignalR
   ▼
ASP.NET Core API ─ Services ─ Repositories ─ EF Core ─ SQL Server
                 └─ SignalR Hub (grupos por partido)
```

---

## 3) Estructura del backend

```
back/Scoreboard/
 ├─ Controllers/            # Auth, Teams, Players, Matches, Standings, Roles, Menu
 ├─ Services/               # AuthService, RoleService, MenuService (+ Interfaces)
 ├─ Repositories/           # Interfaces + implementaciones
 ├─ Models/Entities/        # Team, Player, Match, Foul, ScoreEvent, User, Role, Menu, etc.
 ├─ Data/                   # AppDbContext (Fluent API), utilidades
 ├─ Hubs/                   # ScoreHub (SignalR)
 ├─ Program.cs              # DI, CORS, AuthN/AuthZ, rutas, hubs, etc.
 ├─ appsettings*.json       # ConnectionStrings, Jwt, Cors
 └─ Dockerfile              # Imagen de la API
```

---

## 4) Program.cs y middleware
- **Swagger** habilitado para documentar y probar la API
- **CORS** configurado para permitir el frontend (agregar dominio en producción)
- **EF Core**: registro de `DbContext` contra SQL Server
- **Autenticación/Autorización** con **JWT**
- **SignalR** (ejemplo de mapeo):
```csharp
app.MapHub<ScoreHub>("/hubs/score");
```

---

## 5) Configuración
### 5.1 `appsettings.json` (claves relevantes)
- **ConnectionStrings.DefaultConnection** → cadena a SQL Server (local o contenedor)
- **Cors.AllowedOrigins** → `["http://localhost:4200","http://127.0.0.1:4200"]`
- **Jwt** → `{ "Key", "Issuer", "Audience", "ExpiresInMinutes" }`

> En producción, usa **variables de entorno** en lugar de valores en texto plano.

### 5.2 CORS
Definir la política para permitir el origen del frontend. En producción, agrega tu dominio (p. ej. `https://proyectosdw.lat`).

### 5.3 JWT
- `AuthService` emite tokens con **claims** (usuario/rol)
- Endpoints protegidos requieren `Authorization: Bearer <token>`

---

## 6) Ejecución local (sin Docker)
1. Requisitos: **.NET SDK 8+**, **SQL Server 2022**
2. Configura `DefaultConnection` en `appsettings.json`
3. Restaurar/compilar:
   ```bash
   cd back/Scoreboard
   dotnet restore
   dotnet build
   ```
4. (Opcional) Migraciones/BD:
   ```bash
   # si usas migraciones:
   dotnet ef database update
   ```
5. Ejecutar:
   ```bash
   dotnet run
   ```
6. La API expone `http://localhost:8080` (o según `launchSettings.json`).

---

## 7) Ejecución con Docker Compose
Archivo en la raíz: `docker-compose.yml`

**Servicios típicos**
- `db`: SQL Server 2022 (puerto `1433`), volumen `mssqldata`
- `api`: construye `back/Scoreboard/Dockerfile` y expone `8080:8080`
- `web` (si aplica): frontend

**Comandos**
```bash
docker compose up -d --build    # levantar
docker compose logs -f api      # ver logs de la API
docker compose down             # detener
```

---

## 8) Endpoints / APIs (resumen)
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
- Roles: CRUD
- Menú: `GET /` · `GET /{roleId}` · `POST /role/{roleId}` (asignar) · `GET /mine`

---

## 9) Validaciones y manejo de errores
- Evitar equipos duplicados
- No registrar eventos en **partidos finalizados**
- Verificar que equipos existan al crear partido
- Validar puntos en `ScoreEvent` (1, 2 o 3)
- Validar que la fecha del partido no sea pasada

**Códigos HTTP**
- `200 OK`, `201 Created`, `400 Bad Request`, `404 Not Found`, `500 Internal Server Error`

---

## 10) Base de datos
- **Motor:** SQL Server 2022 (Docker)
- **Conexión:** `DefaultConnection` (en `appsettings.json`)
- **ORM:** EF Core

**Tablas esperadas (resumen)**
- `Teams`, `Players`, `Matches`, `ScoreEvents`, `Fouls`, `TeamWins`
- Seguridad/UI: `Users`, `Roles`, `Menus`, `RoleMenus`

> Migraciones: `dotnet ef migrations add <Nombre>` · `dotnet ef database update`

---

## 11) Lógica de negocio
- **Servicios:** Gestión de equipos, programación de partidos, registro de eventos
- **Críticos:** Actualización en vivo con SignalR y cálculo de posiciones por victorias

---

## 12) Despliegue (VPS/Dominio/Certificado)
- Ejecutar la API detrás de Nginx (reverse proxy) con HTTPS (Let’s Encrypt)
- Agregar `https://proyectosdw.lat` a CORS
- Configurar variables de entorno (ConnectionStrings y JWT)
- Operar con Docker Compose o systemd en modo producción

---

## 13) Troubleshooting
- **401/403** → Token ausente/expirado o rol insuficiente
- **CORS** → Agregar dominio permitido y reiniciar
- **SQL Server** → Ver credenciales/puerto; usar `healthcheck` en Compose
- **SignalR** → Validar URL del hub y `matchId`; permitir WebSockets en Nginx
---

## 14) Lógica de negocio
- **Servicios:** Gestión de equipos, programación de partidos, registro de eventos
- **Críticos:** Actualización en vivo con SignalR y cálculo de posiciones por victorias

---
