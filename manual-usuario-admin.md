# Manual de Usuario — **Rol Administrativo**
## 🏀 Tablero de Baloncesto (Frontend Angular)

**Versión:** 1.0.0  
**Fecha:** 2025-10-15  
**Ámbito:** Uso del sistema desde el navegador con permisos Admin.  
**Requisitos:** Usuario con rol *admin*, navegador moderno, acceso a `https://proyectosdw.lat` (o el dominio que uses).

---

## Tabla de contenidos
1. [Ingreso y rol](#1-ingreso-y-rol)  
2. [Navegación general](#2-navegación-general)  
3. [Gestión de equipos y jugadores](#3-gestión-de-equipos-y-jugadores)  
4. [Gestión de partidos](#4-gestión-de-partidos)  
5. [Marcador en vivo (SignalR)](#5-marcador-en-vivo-signalr)  
6. [Reportería (PDF)](#6-reportería-pdf)  
7. [Gestión de roles y menús](#7-gestión-de-roles-y-menús)  
8. [Sesión y seguridad](#8-sesión-y-seguridad)  
9. [Solución de problemas](#9-solución-de-problemas)  
10. [Checklist previo a demo](#10-checklist-previo-a-demo)  
11. [FAQ rápidas](#11-faq-rápidas)

---

## 1) Ingreso y rol
1. Abre Login: `https://proyectosdw.lat` → *Login*.  
2. Ingresa usuario y contraseña.  
3. Al iniciar sesión, el sistema guarda un token en el navegador y habilita las rutas de administración.  
4. Verifica que tu usuario tenga rol Admin (si no, solicita la asignación al responsable).

> Si tu sesión expira, verás errores 401. Vuelve a iniciar sesión.

---

## 2) Navegación general
- **Menú principal**: *Scoreboard*, *Teams*, *Players*, *Matches*, *Admin / Reports* (los nombres pueden variar levemente).  
- **Encabezado**: acceso a cerrar sesión y, en algunos casos, indicadores de conexión en tiempo real.  
- **Rutas clave**:  
  - `/score/:id` → marcador en vivo del partido **:id**.  
  - `/admin/reports` → centro de reportería (solo Admin).

---

## 3) Gestión de equipos y jugadores
### 3.1 Equipos
- **Crear**: *Teams* → *Nuevo* → completa *Nombre*, *Ciudad*, etc. → *Guardar*.  
- **Editar/Eliminar**: desde el listado de equipos, usa *Editar* o *Eliminar*.  
- **Búsqueda/Orden**: utiliza el buscador y los encabezados de la tabla.

### 3.2 Jugadores
- **Crear**: *Players* → *Nuevo* → asigna al **equipo** y completa los datos → *Guardar*.  
- **Editar/Eliminar**: igual que en equipos.  
-  **Búsqueda/Orden**: utiliza el buscador y los encabezados de la tabla.
- **Validaciones**: evita duplicados y verifica que un jugador pertenezca a un solo equipo.



---

## 4) Gestión de partidos
1. Ir a Matches → *Nuevo* → define **equipo local/visitante**, **fecha/hora**, **lugar**.  
2. Programar el partido (quedará en estado planificado).  
3. Al momento del juego, abre el Scoreboard del partido (ruta `/score/:id`).  
4. Acciones administrativas adicionales (si están disponibles): *Suspender*, *Cancelar* o *Finalizar* partido.

> Evita programar partidos con fecha pasada. Revisa conflictos de horario/equipos.

---

## 5) Marcador en vivo (SignalR)
- El **marcador** usa **WebSockets** (SignalR) en `/hubs/score`.  
- Desde `/score/:id`, el sistema **se conecta al hub** y se une al grupo del partido.  
- Acciones típicas (los nombres de botones pueden variar según la UI):  
  - **Iniciar/Detener** el **reloj** del periodo.  
  - Cambiar **periodo/cuartos**.  
  - Registrar **anotaciones** (+1 / +2 / +3) y **faltas** por jugador.  
  - **Finalizar** el partido (cierra el flujo en vivo).

> Si la conexión WebSocket cae, la app reintentará. Si tarda, revisa Nginx/puertos y conexión a internet.

---

## 6) Reportería (PDF)
Ruta: **Admin → Reports** (`/admin/reports`). Descargas disponibles (según implementación actual):
- **Equipos** → `teams.pdf`
- **Jugadores por equipo** → `team/{id}/players.pdf`
- **Historial de partidos** (con filtros `from`/`to`) → `matches/history.pdf`
- **Roster por partido** → `matches/{id}/roster.pdf`
- **Tabla de posiciones** → `standings.pdf`

**Cómo descargar:**
1. Selecciona el tipo de reporte y, si aplica, filtros (equipo/partido/fechas).  
2. Presiona **Descargar**.  
3. El archivo se guarda como `*.pdf` (verifica que el navegador no bloquee la descarga).

> Si ves 401/403, tu sesión pudo expirar. Si ves 502, revisa que Nginx esté inyectando el JWT interno hacia el servicio de reportes o que el `report-service` esté arriba.

---

## 7) Gestión de roles y menús
- **Roles**: por defecto,  admin. Para reportes y administración, se requiere admin.  
- **Menús**: asigna accesos por rol desde *Admin → Menú* (si la UI lo incluye).  
- **Buenas prácticas**: usa cuentas nominativas; evita compartir contraseñas; revoca accesos de egresos.

---

## 8) Sesión y seguridad
- Cierra sesión desde el menú cuando dejes de usar el sistema.  
- El token se guarda en el navegador (LocalStorage); no lo compartas.  
- Cambia la contraseña periódicamente (si la UI lo soporta) y usa credenciales fuertes.

---

## 9) Solución de problemas
- **No puedo entrar a /admin/reports** → confirma que tu usuario tenga *ol admin* 
- **Descargas fallan (401/502)** → renueva sesión; valida Nginx y token interno de reportes.  
- **Marcador no actualiza** → revisa que `/hubs/score` permita **WebSockets** (cabeceras *Upgrade/Connection*).  
- **CORS en desarrollo** → usa `ng serve --proxy-config proxy.conf.json`.

---

## 10) Checklist previo a demo
- [ ] Equipos y jugadores cargados.  
- [ ] Partidos programados para hoy (o la fecha de la demo).  
- [ ] Scoreboard probado con dos navegadores (ver reflejo en tiempo real).  
- [ ] Reportes descargan OK (sin 401/502).  
- [ ] Dominio/SSL y CORS correctos.

---

## 11) FAQ rápidas
- **¿Puedo editar un partido ya finalizado?** No recomendado; crea uno nuevo o reabre sólo si el flujo lo permite.  
- **¿Usuarios sin rol admin ven reportes?** No, por diseño son solo admin.  
- **¿Puedo usar el sistema desde el móvil?** Sí, la interfaz es responsive (depende de tu layout).

---