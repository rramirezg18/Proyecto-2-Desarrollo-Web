# PROYECTO 1 – DESARROLLO WEB
## 🏀 MARCADOR DE BALONCESTO

**Integrantes**
- Roberto Antonio Ramírez Gómez — 7690-22-12700
- Jean Klaus Castañeda Santos — 7690-22-892
- Jonathan Joel Chan Cuellar — 7690-22-1805

---

# Documentación Técnica – Frontend Angular (Tablero Basket)

## 1) Descripción General
Frontend desarrollado con **Angular (16/17, Standalone Components)** para una SPA que gestiona equipos, jugadores y partidos con **actualización en tiempo real** vía **SignalR**. Consume la **API REST** del backend ASP.NET Core y se construye para producción como sitio estático servido detrás de **Nginx** (o similar).

---

## 2) Tecnologías y paquetes
- **Angular 17+** (Standalone, Router, HttpClient)
- **TypeScript 5+**, **RxJS 7+**
- **Angular Material** y/o **Bootstrap 5**
- **@microsoft/signalr** (cliente SignalR)
- **SweetAlert2 / MatSnackBar** para feedback al usuario
- **SCSS/CSS** (encapsulado por componente)

> Requisitos: **Node.js 18+** y **npm 9+**, **Angular CLI**

```bash
npm i -g @angular/cli
```

---

## 3) Estructura del proyecto
Ubicación típica del cliente:

```
front/scoreboard/
└─ src/
   ├─ app/
   │  ├─ core/                 # Servicios base, interceptores, guards
   │  │  ├─ api/               # Servicios HTTP (equipos, partidos, auth, etc.)
   │  │  ├─ realtime/          # Servicio SignalR
   │  │  ├─ services/          # AuthenticationService, StorageService, etc.
   │  │  ├─ guards/            # AuthGuard, RoleGuard
   │  │  └─ interceptors/      # AuthInterceptor (JWT)
   │  ├─ pages/                # Vistas principales (scoreboard, matches, teams, admin)
   │  ├─ shared/               # Componentes compartidos (topbar, timer, team-panel, fouls-panel...)
   │  ├─ app.routes.ts         # Ruteo principal (Standalone)
   │  └─ app.config.ts         # Providers (HttpClient, Interceptors, etc.)
   ├─ assets/                  # Imágenes, fuentes, estilos globales
   ├─ environments/            # environment.ts / environment.prod.ts
   ├─ main.ts                  # Bootstrap de la app
   ├─ styles.scss              # Estilos globales
   ├─ index.html
   ├─ angular.json
   ├─ package.json
   └─ proxy.conf.json          # Proxy de dev para /api
```

Componentes clave (ejemplos reales del proyecto):
- `TopbarComponent` · `TeamPanelComponent` · `TimerComponent` · `QuarterIndicatorComponent` · `FoulsPanelComponent`
- Páginas: `scoreboard`, `tournaments`, `teams`, `matches`, `admin`

---

## 4) Enrutamiento (Standalone)
Ejemplo de definición con guards y lazy loading por rutas:

```ts
// src/app/app.routes.ts
import { Routes } from '@angular/router';
import { AuthGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'scoreboard', pathMatch: 'full' },
  {
    path: 'scoreboard',
    loadComponent: () => import('./pages/scoreboard/scoreboard').then(m => m.ScoreboardComponent)
  },
  {
    path: 'teams',
    canActivate: [AuthGuard],
    loadComponent: () => import('./pages/teams/teams').then(m => m.TeamsComponent)
  },
  {
    path: 'matches',
    canActivate: [AuthGuard],
    loadComponent: () => import('./pages/matches/matches').then(m => m.MatchesComponent)
  },
  { path: '**', redirectTo: 'scoreboard' }
];
```

---

## 5) Comunicación con el backend
### 5.1 Environments
```ts
// src/environments/environment.ts
export const environment = {
  production: false,
  apiBase: 'http://localhost:8080',
  signalRHub: '/hubs/score'
};
```

```ts
// src/environments/environment.prod.ts
export const environment = {
  production: true,
  apiBase: 'https://proyectosdw.lat',   // backend en producción
  signalRHub: '/hubs/score'
};
```

### 5.2 Proxy de desarrollo (opcional)
```json
// proxy.conf.json
{
  "/api": {
    "target": "http://localhost:8080",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "debug"
  }
}
```
Ejecutar con proxy:
```bash
ng serve --proxy-config proxy.conf.json
```

### 5.3 Interceptor JWT
```ts
// src/app/core/interceptors/auth.interceptor.ts
import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('token');
  const cloned = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;
  return next(cloned);
};
```

### 5.4 Servicio API (ejemplo)
```ts
// src/app/core/api/teams.api.ts
import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class TeamsApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/api/teams`;

  list()   { return this.http.get(`${this.base}`); }
  get(id: number) { return this.http.get(`${this.base}/${id}`); }
  create(dto: any) { return this.http.post(this.base, dto); }
  update(id: number, dto: any) { return this.http.put(`${this.base}/${id}`, dto); }
  remove(id: number) { return this.http.delete(`${this.base}/${id}`); }
}
```

### 5.5 Servicio SignalR (resumen)
```ts
// src/app/core/realtime/realtime.service.ts
import * as signalR from '@microsoft/signalr';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private connection?: signalR.HubConnection;

  connect(matchId: number) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiBase}${environment.signalRHub}?matchId=${matchId}`)
      .withAutomaticReconnect()
      .build();

    this.connection.on('scoreChanged', (payload) => {
      // actualizar estado/servicios
      console.log('scoreChanged', payload);
    });

    return this.connection.start();
  }

  disconnect() {
    return this.connection?.stop();
  }
}
```

---

## 6) Autenticación y autorización (frontend)
- **Login**: `AuthenticationService` llama a `/api/auth/login`, guarda **token** y **claims** (rol) en `localStorage`.
- **AuthGuard**: protege rutas si no hay token válido.
- **RoleGuard** (opcional): permite rutas solo a `Admin` u otros roles.
- **Feedback**: mostrar errores con `MatSnackBar` o `SweetAlert2`.

```ts
// src/app/core/guards/auth.guard.ts
import { CanActivateFn, Router } from '@angular/router';

export const AuthGuard: CanActivateFn = () => {
  const token = localStorage.getItem('token');
  return !!token; // mejora: validar expiración
};
```

---

## 7) Estilos y diseño
- **Encapsulación por componente** (`.scss` o `.css`) para evitar colisiones.
- **Grid/Responsive** con Bootstrap o Angular Material Layout.
- **Variables CSS** para tema del marcador (colores LEDs, fondo, etc.).
- **Buenas prácticas**: no usar `!important` salvo imprescindible; evitar estilos globales agresivos.

Ejemplo de layout con Bootstrap:
```html
<div class="container py-3">
  <div class="row g-3">
    <div class="col-12 col-md-6">Equipo Local</div>
    <div class="col-12 col-md-6">Equipo Visitante</div>
  </div>
</div>
```

---

## 8) Instalación y ejecución
```bash
# en la carpeta front/scoreboard
npm install

# desarrollo (con proxy a /api)
ng serve --proxy-config proxy.conf.json

# producción
ng build --configuration production
# salida: dist/scoreboard/ (o nombre del proyecto)
```

---

## 9) Despliegue (opción Nginx + Docker)
**Dockerfile (multi-stage)**
```Dockerfile
# Etapa de build
FROM node:20 AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration production

# Etapa de runtime (estático)
FROM nginx:alpine
COPY --from=build /app/dist/ /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

**nginx.conf (ejemplo básico)**
```nginx
server {
  listen 80;
  server_name _;

  root /usr/share/nginx/html;
  index index.html;

  location / {
    try_files $uri $uri/ /index.html;
  }
}
```

> Si la API está en otro host/domino, habilitar **CORS** en backend y/o usar proxy inverso.

---

## 10) Buenas prácticas implementadas
- Separación de responsabilidades: **pages / shared / core**
- **Interfaces** TS para tipado fuerte de modelos
- **Interceptors** para tokens y manejo de errores
- **Environments** para URLs y hubs
- **Lazy loading** por rutas (mejora de rendimiento)
- **Encapsulación de estilos** por componente

---

## 11) Troubleshooting
- **CORS**: usar `proxy.conf.json` en dev; en prod, configurar CORS en backend y Nginx
- **401/403**: token faltante/expirado; revisar interceptor y almacenamiento
- **SignalR**: error de negociación → verificar URL, habilitar WebSockets en proxy
- **404 al refrescar ruta**: configurar `try_files ... /index.html` en Nginx
- **Error Angular Material**: falta importar módulo/componente específico
- **Build**: versiones Node/CLI incompatibles → usar Node 18+ y Angular CLI alineada

---

## 12) Mapa de pantallas (resumen)
- **Login** → autenticación JWT
- **Scoreboard** → marcador en vivo (timer, periodo, faltas, puntos)
- **Teams** → CRUD equipos y jugadores
- **Matches** → programación y seguimiento de partidos
- **Admin** → menú/roles (según permisos)
