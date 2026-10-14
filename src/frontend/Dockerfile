# =============================================================================
# SNCEV Frontend — Dockerfile para despliegue local y producción
# =============================================================================

# Etapa 1: Compilación de los archivos de React + Vite
FROM node:20-alpine AS builder

WORKDIR /app

COPY package.json ./
COPY package-lock.json* ./

RUN npm install --legacy-peer-deps

COPY . .

ARG VITE_API_URL=/api/v1
ARG VITE_BACKEND_URL=http://localhost:8000
ENV VITE_API_URL=$VITE_API_URL
ENV VITE_BACKEND_URL=$VITE_BACKEND_URL

RUN npm run build

# Etapa 2: Servidor web ultraligero con Nginx
FROM nginx:alpine

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=builder /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
