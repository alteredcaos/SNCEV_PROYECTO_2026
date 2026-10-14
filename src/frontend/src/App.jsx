import { Toaster } from '@/components/ui/toaster';
import { QueryClientProvider } from '@tanstack/react-query';
import { queryClientInstance } from '@/lib/query-client';
import { BrowserRouter as Router, Navigate, Route, Routes } from 'react-router-dom';
import PageNotFound from './lib/PageNotFound';
import { AuthProvider } from '@/lib/AuthContext';
import ScrollToTop from './components/ScrollToTop';
import ProtectedRoute from '@/components/ProtectedRoute';
import { ROLES, TODOS_LOS_ROLES } from '@/lib/roles';

import Layout from '@/components/Layout';
import { RoleProvider } from '@/lib/RoleContext';
import Dashboard from '@/pages/Dashboard';
import Pacientes from '@/pages/Pacientes';
import PacienteDetalle from '@/pages/PacienteDetalle';
import Vacunacion from '@/pages/Vacunacion';
import Citas from '@/pages/Citas';
import Cartilla from '@/pages/Cartilla';
import Reportes from '@/pages/Reportes';
import Administracion from '@/pages/Administracion';
import Login from '@/pages/Login';
import ExpedienteInactivo from "@/pages/ExpedienteInactivo";
import Conflictos from '@/pages/Conflictos';

const CLINICA = [ROLES.ADMINISTRADOR, ROLES.CAPTURADOR];
const CONSULTA = [ROLES.ADMINISTRADOR, ROLES.CAPTURADOR, ROLES.DIRECTIVO, ROLES.AUDITOR];
const REPORTES = [ROLES.ADMINISTRADOR, ROLES.DIRECTIVO, ROLES.AUDITOR];
const ADMIN = [ROLES.ADMINISTRADOR];
const EXPEDIENTE_INACTIVO = [
  ROLES.ADMINISTRADOR,
  ROLES.AUDITOR
];
const AppRoutes = () => (
  <Routes>
    <Route path="/login" element={<Login />} />

    <Route element={<ProtectedRoute roles={TODOS_LOS_ROLES} />}>
      <Route element={<RoleProvider><Layout /></RoleProvider>}>
        <Route path="/" element={<Dashboard />} />

        <Route element={<ProtectedRoute roles={CONSULTA} />}>
          <Route path="/pacientes" element={<Pacientes />} />
          <Route path="/pacientes/:id" element={<PacienteDetalle />} />
          <Route path="/cartilla" element={<Cartilla />} />
        </Route>

        <Route element={<ProtectedRoute roles={CLINICA} />}>
          <Route path="/vacunacion" element={<Vacunacion />} />
          <Route path="/citas" element={<Citas />} />
        </Route>

        <Route element={<ProtectedRoute roles={REPORTES} />}>
          <Route path="/reportes" element={<Reportes />} />
        </Route>

       <Route element={<ProtectedRoute roles={ADMIN} />}>
  <Route path="/administracion" element={<Administracion />} />
  <Route
    path="/administracion/conflictos"
    element={<Conflictos />}
  />
</Route>
        <Route element={<ProtectedRoute roles={EXPEDIENTE_INACTIVO} />}>
  <Route
    path="/administracion/pacientes-inactivos/:id"
    element={<ExpedienteInactivo />}
  />
</Route>
      </Route>
    </Route>

    <Route path="/register" element={<Navigate to="/login" replace />} />
    <Route path="/forgot-password" element={<Navigate to="/login" replace />} />
    <Route path="/reset-password" element={<Navigate to="/login" replace />} />
    <Route path="*" element={<PageNotFound />} />
  </Routes>
);

function App() {
  return (
    <AuthProvider>
      <QueryClientProvider client={queryClientInstance}>
        <Router>
          <ScrollToTop />
          <AppRoutes />
        </Router>
        <Toaster />
      </QueryClientProvider>
    </AuthProvider>
  );
}

export default App;
