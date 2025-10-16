# PROYECTO 3 – DESARROLLO WEB
## 🏀 MARCADOR DE BALONCESTO

**Integrantes**
- Roberto Antonio Ramírez Gómez — 7690-22-12700
- Jean Klaus Castañeda Santos — 7690-22-892
- Jonathan Joel Chan Cuellar — 7690-22-1805

---

# Manual Técnico – **Frontend Angular** (Tablero de Baloncesto) 

**Versión:** 1.1.0  
**Fecha:** 2025-10-15  
**Stack:** Angular 20 (Standalone + SSR opcional) · TypeScript 5.8 · RxJS 7.8 · Angular Material 20 · @microsoft/signalr 9 · SweetAlert2 · Nginx (reverse proxy)


---

## Tabla de contenidos
1. [Descripción General](#1-descripción-general)  
2. [Stack y Versiones](#2-stack-y-versiones)  
3. [Estructura del Proyecto](#3-estructura-del-proyecto)  
4. [Configuración de Entornos](#4-configuración-de-entornos)  
5. [Rutas y Guards](#5-rutas-y-guards)  
6. [Interceptors](#6-interceptors)  
7. [Servicios API](#7-servicios-api)  
8. [Tiempo Real (SignalR)](#8-tiempo-real-signalr)  
9. [UI/Estilos](#9-uiestilos)  
10. [Ejecución Local](#10-ejecución-local)  
11. [Despliegue (Nginx + SPA / SSR opcional)](#11-despliegue-nginx--spa--ssr-opcional)  
12. [Reportería desde el Frontend](#12-reportería-desde-el-frontend)  
13. [Seguridad en Frontend](#13-seguridad-en-frontend)  
14. [Observabilidad y UX de Errores](#14-observabilidad-y-ux-de-errores)  
15. [Troubleshooting](#15-troubleshooting)  
16. [Versionado y Licencia](#16-versionado-y-licencia)

---

## 1) Descripción General
SPA en **Angular 20** (Standalone Components) que consume la **API REST** (`/api/*`) y el microservicio de reportes (`/api/reports/*`). La actualización en tiempo real del marcador se realiza vía SignalR con hub en `/hubs/score`.  
El frontend puede desplegarse como SPA estática detrás de Nginx o, opcionalmente, usando SSR (Node/Express) incluido en el proyecto.

---

## 2) Stack y Versiones
- **Angular**: 20.1.x (`@angular/core`, `@angular/router`, `@angular/ssr`)  
- **Angular Material/CDK**: 20.1.x  
- **TypeScript**: `~5.8.2`  
- **RxJS**: `~7.8.0`  
- **SignalR cliente**: `@microsoft/signalr ^9.0.6`  
- **SweetAlert2**: `^11.22.4`  
- **Node**: 20+ recomendado; Angular CLI alineado a Angular 20.  
- **Scripts** (`package.json`): `start` (dev), `build`, `serve:ssr:scoreboard` (sirve el bundle SSR `dist/scoreboard/server/server.mjs`).

---

## 3) Estructura del Proyecto
Raíz del frontend: `front/scoreboard/`
```
front/scoreboard/
 ├─ angular.json
 ├─ package.json
 ├─ proxy.conf.json
 ├─ public/
 └─ src/
    ├─ app/
    │  ├─ core/                 # api.ts, realtime.ts, guards, interceptors, services
    │  ├─ components/           # componentes reutilizables
    │  ├─ features/             # módulos/líneas de funcionalidad (scoreboard, etc.)
    │  ├─ pages/                # vistas (login, admin/reports, teams, matches, ...)
    │  ├─ models/               # interfaces TS de dominio
    │  ├─ services/             # (si aplica) servicios de páginas
    │  ├─ app.routes.ts         # ruteo principal (Standalone)
    │  ├─ app.config.ts         # providers globales (HttpClient, interceptors, ...)
    │  ├─ app.html / app.scss   # shell de la app
    │  └─ app.ts                # bootstrap
    ├─ enviroments/             # ⚠️ carpeta (con typo) `enviroments.ts`
    ├─ index.html
    ├─ main.ts                  # bootstrap browser
    ├─ main.server.ts           # bootstrap SSR
    ├─ server.ts                # servidor Express para SSR
    └─ styles.scss              # estilos globales
```

**Notas**  
- La carpeta de entornos es `src/**enviroments**/enviroments.ts` (nombre con typo en el repo).  
- Hay soporte SSR (ficheros `main.server.ts` y `server.ts`).

---

## 4) Configuración de Entornos
Archivo: `src/enviroments/enviroments.ts`
```ts
// environment.ts / environment.prod.ts
export const environment = {
  production: true,
  apiBaseUrl: '/api',
  hubUrl: '/hubs/score'
};
```
- `apiBaseUrl`: base para REST (`/api`).  
- `hubUrl`: ruta del Hub de SignalR.  
 En producción detrás de Nginx, estas rutas relativas funcionan bien. Para desarrollo local con backend en otro puerto, usar proxy.

**Proxy de desarrollo** – `proxy.conf.json`
```json
{
  "/api": { "target": "http://localhost:8080", "secure": false, "changeOrigin": true, "logLevel": "debug" },
  "/hubs": { "target": "http://localhost:8080", "ws": true, "secure": false, "changeOrigin": true }
}
```

Ejecutar con proxy:
```bash
ng serve --proxy-config proxy.conf.json
```

---

## 5) Rutas y Guards
Archivo: `src/app/app.routes.ts` (extracto)
```ts
import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },

  { path: 'login', loadComponent: () => import('./pages/login/login').then(m => m.LoginComponent) },

  // Administración de reportes (sólo Admin)
  {
    path: 'admin/reports',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./pages/admin/reports/reports-page').then(m => m.ReportsPage)
  },

  // Scoreboard y control (autenticado)
  {
    path: 'score/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/scoreboard/scoreboard/scoreboard').then(m => m.ScoreboardComponent),
  },

  // Torneos (sólo Admin), Teams, Matches, etc. (similares)
  { path: '**', redirectTo: 'login' },
];
```
**Guards** (`core/guards/*.ts`)  
- `authGuard`: deja pasar si hay token (la validez final la decide el backend; si falta token redirige a `/login`).  
- `adminGuard`: verifica `role=admin` en el JWT (ver `AuthenticationService.isAdmin()`), si no, redirige a `/login?returnUrl=`.

---

## 6) Interceptors
Archivo: `core/interceptors/auth-token.interceptor.ts`  
- Inyecta el Bearer: `Authorization: Bearer <token>` en todas las peticiones.  
- Maneja 401: redirige a `/login` sin limpiar storage (el logout lo hace el usuario o el backend por expiración).

```ts
export const authTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const token = auth.getToken();
  const authReq = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(authReq).pipe(/* redirect 401 */);
};
```

Registrar en `app.config.ts` (providers).

---

## 7) Servicios API
### 7.1 `ApiService` (REST base)
Archivo: `core/api.ts`
```ts
@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = '/api'; // usa environment.apiBaseUrl en servicios específicos si prefieres

  getMatch(id: number) { return this.http.get<any>(`${this.base}/matches/${id}`); }
  // ...otros métodos: teams, players, standings, score, etc.
}
```

### 7.2 `AuthenticationService`
Archivo: `core/services/authentication.service.ts`  
- `login(username, password)` llama a `/api/auth/login` y guarda `token`/`user` en `localStorage`.  
- `getToken()`, `isAdmin()` (acepta `role` o `roles[]` en el payload).  
- `logout()` limpia `user/token`.

### 7.3 `ReportsService` (reportería PDF)
Archivo: `core/services/reports.service.ts`  
Base: `/api/reports` → Nginx → **report-service** (FastAPI).
```ts
downloadTeams()                => GET /api/reports/teams.pdf
downloadTeamPlayers(teamId)    => GET /api/reports/team/{id}/players.pdf
downloadMatches(from,to)       => GET /api/reports/matches/history.pdf?from=...&to=...
downloadMatchRoster(matchId)   => GET /api/reports/matches/{id}/roster.pdf
downloadStandings()            => GET /api/reports/standings.pdf
```

---

## 8) Tiempo Real (SignalR)
Archivo: `core/realtime.ts`  
- Crea `HubConnection` a `${environment.apiBaseUrl}${environment.hubUrl}`.  
- Señales (`signal`) para score, timeLeft, timerRunning, quarter, timeouts, gameOver.  
- Handlers de eventos: `scoreChanged`, `foulCommitted`, `gameEnded`, etc.  
- Métodos `connect(matchId)`, `disconnect()`, `startTick()` (temporizador local), `stopTick()`.

> Nginx debe habilitar WebSockets para `/hubs/score` (Upgrade/Connection).

---

## 9) UI/Estilos
- **Angular Material** como base de componentes; estilos globales en `src/styles.scss` y `src/app/app.scss`.  
- Componentes reutilizables en `src/app/components/` y `src/app/shared/`.  
- Mantener encapsulación por componente; usar utilidades CSS y variables para temas del marcador.

---

## 10) Ejecución Local
```bash
cd front/scoreboard
npm ci
# Desarrollo con proxy a backend en :8080 (incluye WS /hubs):
ng serve --proxy-config proxy.conf.json
# Build producción (SPA estática):
npm run build
```

> Salida de build: `dist/scoreboard/` (browser) y, si se genera SSR, `dist/scoreboard/server/` (Node).

---

## 11) Despliegue (Nginx + SPA / SSR opcional)

### A) **SPA estática** detrás de Nginx (recomendado)
**Dockerfile**
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

**nginx.conf** (SPA + proxy a API/Reportes + WS)
```nginx
server {
  listen 80;
  server_name _;

  root /usr/share/nginx/html;
  index index.html;

  # API principal (.NET)
  location /api/ {
    proxy_pass http://api:8080/;
    proxy_http_version 1.1;
  }

  # Reportes (FastAPI)
  location /api/reports/ {
    proxy_pass http://report-service:8080/;
    proxy_http_version 1.1;
    # (si aplica) Authorization: Bearer <JWT_RS_INTERNO> inyectado por Nginx
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
}
```

### B) SSR 
- Build SSR (Angular 20 + `@angular/ssr` genera `dist/scoreboard/server/server.mjs`).  
- Ejecutar el servidor Node/Express (`src/server.ts` → puerto `4000` por defecto si `PORT` no está seteado).  
- Nginx como reverse proxy al SSR para `/` y proxies a `/api/*`, `/api/reports/*`, `/hubs/score` igual que arriba.

 Nginx (SSR al frontend)
```nginx
location / {
  proxy_pass http://frontend-ssr:4000/;
  proxy_http_version 1.1;
}
```

---

## 12) Reportería desde el Frontend
Página: `src/app/pages/admin/reports/reports-page.*`  
Botones para descargar PDFs invocan `ReportsService` y guardan con `Blob`. Manejo de errores:
```ts
if (e.status === 401) return 'No autorizado (revisa sesión / token).';
if (e.status === 502) return 'Gateway error (¿Nginx inyecta el RS_TOKEN?).';
if (e.status === 500) return 'Error del servidor de reportes.';
```

---

## 13) Seguridad en Frontend
- **Storage**: `AuthenticationService` guarda `token`/`user` en `localStorage` (sólo en navegador; check `isPlatformBrowser`).  
- **Guards**: `authGuard` (exige token), `adminGuard` (exige `role=admin` en JWT).  
- **Interceptor**: agrega `Authorization` y redirige en `401` (no hace logout automático).  
- **Buenas prácticas**: evitar exponer secretos en el frontend; no persistir datos sensibles; expirar sesión por inactividad desde backend.

---

## 14) Observabilidad y UX de Errores
- Notificaciones: `MatSnackBar` o **SweetAlert2** (incluida).  
- Registros de consola moderados en prod; usar `environment.production` para silenciar verbose logs.  
- Capturar errores HTTP a nivel de servicios y mostrar mensajes entendibles (como en `ReportsPage`).

---

## 15) Troubleshooting
- **CORS**: en dev usar `proxy.conf.json`; en prod, configurar CORS en backend y Nginx.  
- **401/Redirect loop**: revisar `authTokenInterceptor` y que el backend no invalide el token inmediatamente.  
- **SignalR falla**: confirmar `Upgrade/Connection` en Nginx y la URL `${apiBaseUrl}${hubUrl}`.  
- **404 al refrescar**: `try_files $uri $uri/ /index.html;` en Nginx (SPA). En SSR, proxy a `/:4000`.  
- **Descarga de PDFs falla (502/401)**: Nginx debe inyectar el **JWT interno de reportes** o configurar `UPSTREAM_TOKEN` para el `report-service`.

---

