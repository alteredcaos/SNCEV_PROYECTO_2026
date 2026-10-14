# SNCEV — Frontend (React + Vite + Tailwind CSS)

Interfaz web del **Sistema Nacional de Cartilla Electrónica de Vacunación (SNCEV)**.

Permite la gestión de pacientes, registro y aplicación de vacunas, visualización de la cartilla electrónica con código QR, gestión de citas médicas, reportes epidemiológicos y cobertura de vacunación por entidad/jurisdicción/municipio, y administración de usuarios con control de acceso basado en roles (RBAC: Vacunador y Coordinador).

---

## 🚀 Requisitos

- **Node.js** 18+ o 20+ (LTS recomendado) y **npm**
- O **Docker** y **Docker Compose**

---

## 🛠️ Ejecución en Desarrollo Local

### Opción 1: Con Node.js y npm

1. **Instalar dependencias:**
   ```bash
   npm install --legacy-peer-deps
   ```

2. **Configurar variables de entorno (opcional):**
   ```bash
   cp .env.example .env
   ```
   Por defecto, Vite reenviará las llamadas a `/api` hacia `http://localhost:8000` (FastAPI).

3. **Iniciar el servidor de desarrollo:**
   ```bash
   npm run dev
   ```
   Abre [http://localhost:5173](http://localhost:5173) en tu navegador.

### Opción 2: Con Docker (incluido en docker-compose)

Desde la raíz del proyecto `/home/mrrobot/proyectos/SNCEV`:
```bash
docker compose up -d frontend
```
El frontend quedará accesible en [http://localhost:3000](http://localhost:3000).

---

## 📦 Construcción para Producción

Para compilar los archivos estáticos listos para cualquier servidor web (Nginx, Apache, Caddy, Vercel, Netlify):

```bash
npm run build
```

Los artefactos listos para servir se generarán en la carpeta `dist/`.

---

## 📂 Estructura del Frontend

- `src/api/apiClient.js`: Cliente HTTP centralizado para comunicación con la API REST (`/api/v1`).
- `src/lib/AuthContext.jsx`: Contexto de autenticación y manejo de sesión local y remota.
- `src/lib/RoleContext.jsx`: Conmutador dinámico de roles de usuario (Vacunador / Coordinador).
- `src/lib/mockData.js`: Catálogo y datos demostrativos de pacientes, vacunas, dosis y coberturas.
- `src/pages/`:
  - `Dashboard.jsx`: Métricas clave (cobertura, citas del día, alertas de lote).
  - `Pacientes.jsx`: Búsqueda y listado de pacientes por CURP o nombre.
  - `PacienteDetalle.jsx`: Expediente clínico y somatometría del paciente.
  - `Vacunacion.jsx`: Registro de aplicación de vacunas, lote y aplicador.
  - `Cartilla.jsx`: Visualización de la cartilla electrónica y validación QR.
  - `Citas.jsx`: Calendario y programación de citas.
  - `Reportes.jsx`: Gráficas de cobertura y rezago vacunal.
  - `Administracion.jsx`: Bitácora de auditoría y gestión de personal.
  - `Login.jsx`: Pantalla de inicio de sesión institucional.
