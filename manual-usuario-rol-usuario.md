# Manual de Usuario — **Rol Usuario**
## 🏀 Tablero de Baloncesto (Frontend Angular)

**Versión:** 1.0.0  
**Fecha:** 2025-10-15  
**Ámbito:** Uso del sistema desde el navegador con rol Usuario (sin privilegios administrativos).

---

## Tabla de contenidos
1. [Ingreso](#1-ingreso)  
2. [Navegación](#2-navegación)  
3. [Ver partidos y marcador](#3-ver-partidos-y-marcador)  
4. [Tabla de posiciones](#4-tabla-de-posiciones)  
5. [Mi sesión](#5-mi-sesión)  
6. [Preguntas frecuentes](#6-preguntas-frecuentes)  
7. [Solución de problemas](#7-solución-de-problemas)

---

## 1) Ingreso
1. Abre `https://proyectosdw.lat`.  
2. Ve a Login e ingresa tus credenciales.  
3. Si el inicio de sesión es correcto, verás el menú principal.

> Si tu sesión expira, vuelve a iniciar sesión (verás errores 401 si el token ya no es válido).

---

## 2) Navegación
- **Menú**: acceso a *Partidos*, *Scoreboard* y *Standings* (los nombres pueden variar).  
- **Restricción**: las opciones Admin (como *Reports*) no están disponibles para usuarios sin ese rol.

---

## 3) Ver partidos y marcador
- **Partidos**: desde *Matches/Partidos* puedes ver el listado y detalles (fecha, equipos, estado).  
- **Marcador en vivo**: al abrir un partido, la pantalla /score/:id muestra el marcador, periodo y tiempo restante.  
- La actualización es en tiempo real mediante SignalR (no necesitas refrescar la página).

> Como *Usuario*,  puedes editar marcadores y registrar puntos/faltas. 

---

## 4) Tabla de posiciones
- En *Standings* verás la tabla de posiciones por victorias/derrotas (cuando esté habilitada).  
- Se actualiza automáticamente según los resultados.

---

## 5) Mi sesión
- **Cerrar sesión**: usa el menú superior para salir del sistema.  
- **Seguridad**: no compartas tus credenciales; cierra sesión en equipos públicos.

---

## 6) Preguntas frecuentes
- **¿Puedo descargar reportes PDF?** No, esa opción es solo para Administradores.  
- **¿Puedo ver el marcador desde el teléfono?** Sí, la interfaz es responsive .  
- **¿Por qué a veces el marcador tarda en actualizar?** Puede ser la conexión; la app reintenta la conexión al *hub* de tiempo real.

---

## 7) Solución de problemas
- **No carga la página / marca 401** → tu sesión expiró; vuelve a iniciar sesión.  
- **El marcador no se mueve** → verifica tu conexión; si persiste, avisa al Admin (podrían estar caídos WebSockets).  
- **No veo reportes** → es normal; no tienes permisos de Admin.

---