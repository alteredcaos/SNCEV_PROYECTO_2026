import React, { useState } from "react";
import { Outlet, NavLink, useNavigate } from "react-router-dom";
import {
  LayoutDashboard,
  Users,
  Syringe,
  CalendarDays,
  FileText,
  BarChart3,
  ShieldCheck,
  Search,
  Menu,
  X,
  Stethoscope,
  GitMerge,
} from "lucide-react";import { useRole } from "@/lib/RoleContext";
import { useAuth } from "@/lib/AuthContext";
import { ROLES } from "@/lib/roles";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

const navItems = [
  { to: "/", label: "Panel", icon: LayoutDashboard, roles: [ROLES.ADMINISTRADOR, ROLES.CAPTURADOR, ROLES.DIRECTIVO, ROLES.AUDITOR] },
  { to: "/pacientes", label: "Pacientes", icon: Users, roles: [ROLES.ADMINISTRADOR, ROLES.CAPTURADOR, ROLES.DIRECTIVO, ROLES.AUDITOR] },
  { to: "/vacunacion", label: "Vacunación", icon: Syringe, roles: [ROLES.ADMINISTRADOR, ROLES.CAPTURADOR] },
  { to: "/citas", label: "Citas", icon: CalendarDays, roles: [ROLES.ADMINISTRADOR, ROLES.CAPTURADOR] },
  { to: "/cartilla", label: "Cartilla", icon: FileText, roles: [ROLES.ADMINISTRADOR, ROLES.CAPTURADOR, ROLES.DIRECTIVO, ROLES.AUDITOR] },
  { to: "/reportes", label: "Reportes", icon: BarChart3, roles: [ROLES.ADMINISTRADOR, ROLES.DIRECTIVO, ROLES.AUDITOR] },
  { to: "/administracion", label: "Administración", icon: ShieldCheck, roles: [ROLES.ADMINISTRADOR] },
  {
  to: "/administracion/conflictos",
  label: "Conflictos Sync",
  icon: GitMerge,
  roles: [ROLES.ADMINISTRADOR]
},
];

export default function Layout() {
  const { role } = useRole();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);

  const items = navItems.filter((i) => i.roles.includes(role));

  const goSearch = (e) => {
    e.preventDefault();
    navigate("/pacientes");
  };

  return (
    <div className="min-h-screen bg-muted/30 flex">
      {/* Sidebar */}
      <aside
        className={cn(
          "fixed lg:static inset-y-0 left-0 z-40 w-64 bg-sidebar text-sidebar-foreground border-r border-sidebar-border flex flex-col transition-transform",
          open ? "translate-x-0" : "-translate-x-full lg:translate-x-0"
        )}
      >
        <div className="h-16 flex items-center gap-2 px-5 border-b border-sidebar-border">
          <div className="w-9 h-9 rounded-lg bg-primary text-primary-foreground flex items-center justify-center">
            <Stethoscope className="w-5 h-5" />
          </div>
          <div className="leading-tight">
            <p className="font-heading font-semibold text-sm">SNCEV</p>
            <p className="text-[11px] text-muted-foreground">Cartilla Electrónica</p>
          </div>
          <button className="ml-auto lg:hidden" onClick={() => setOpen(false)}>
            <X className="w-5 h-5" />
          </button>
        </div>

        <nav className="flex-1 p-3 space-y-1 overflow-y-auto">
          {items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/"}
              onClick={() => setOpen(false)}
              className={({ isActive }) =>
                cn(
                  "flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors",
                  isActive
                    ? "bg-sidebar-primary text-sidebar-primary-foreground"
                    : "text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground"
                )
              }
            >
              <item.icon className="w-4.5 h-4.5" />
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div className="p-3 border-t border-sidebar-border">
          <Button variant="ghost" className="w-full justify-start" onClick={() => { logout(); navigate('/login'); }}>
            Cerrar sesión
          </Button>
        </div>
      </aside>

      {open && (
        <div className="fixed inset-0 bg-black/40 z-30 lg:hidden" onClick={() => setOpen(false)} />
      )}

      {/* Main */}
      <div className="flex-1 flex flex-col min-w-0">
        <header className="h-16 border-b bg-background flex items-center gap-3 px-4 lg:px-6 sticky top-0 z-20">
          <button className="lg:hidden" onClick={() => setOpen(true)}>
            <Menu className="w-5 h-5" />
          </button>
          <form onSubmit={goSearch} className="relative max-w-md w-full hidden sm:block">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <Input placeholder="Buscar paciente por CURP o nombre…" className="pl-9" />
          </form>
          <div className="ml-auto flex items-center gap-3">
            <div className="text-right hidden sm:block">
              <p className="text-sm font-medium leading-tight">
                {user?.nombreCompleto || user?.nombre_completo || user?.nombre || user?.username || "Usuario"}
              </p>
              <p className="text-[11px] text-muted-foreground">{role || "Sin rol"}</p>
            </div>
            <div className="w-9 h-9 rounded-full bg-primary text-primary-foreground flex items-center justify-center text-sm font-semibold">
              {(user?.nombreCompleto || user?.nombre || user?.username || "U").split(" ").map((x) => x[0]).slice(0, 2).join("").toUpperCase()}
            </div>
          </div>
        </header>

        <main className="flex-1 p-4 lg:p-6 overflow-x-hidden">
          <Outlet />
        </main>
      </div>
    </div>
  );
}