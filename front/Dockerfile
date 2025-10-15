# ===== Build Angular =====
FROM node:20-alpine AS build
WORKDIR /app
COPY scoreboard/package*.json ./scoreboard/
WORKDIR /app/scoreboard
COPY scoreboard/ .
RUN npm ci || npm install
RUN npx -y -p @angular/cli@20 ng build -c production

# ===== Nginx (runtime) =====
FROM nginx:alpine
WORKDIR /usr/share/nginx/html
RUN rm -rf ./*

# Copia el artefacto Angular
COPY --from=build /app/scoreboard/dist/scoreboard/browser/ ./

# Copia tu configuración directa de Nginx
COPY nginx.conf /etc/nginx/conf.d/default.conf
