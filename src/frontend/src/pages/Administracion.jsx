import React, { useEffect, useMemo, useState } from "react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  Table, TableHeader, TableBody, TableHead, TableRow, TableCell,
} from "@/components/ui/table";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from "@/components/ui/dialog";
import { UserPlus, ShieldCheck, Search, RotateCcw, Loader2, UserX, Eye } from "lucide-react";
import { usuarios, roles } from "@/lib/mockData";
import { useToast } from "@/components/ui/use-toast";
import { apiClient } from "@/api/apiClient";
import { useNavigate } from "react-router-dom";

const iniciales = (n = "") => n.split(" ").filter(Boolean).map((w) => w[0]).slice(0, 2).join("").toUpperCase();

const value = (obj, camel, snake) => {
  if (!obj) return null;
  const pascal = camel.charAt(0).toUpperCase() + camel.slice(1);
  return obj[camel] ?? obj[pascal] ?? obj[snake] ?? null;
};

const nombrePaciente = (p) => [
  value(p, "nombre", "nombre"),
  value(p, "primerApellido", "primer_apellido"),
  value(p, "segundoApellido", "segundo_apellido"),
].filter(Boolean).join(" ");

function fechaHoraMX(fecha) {
  if (!fecha) return "—";
  const d = new Date(fecha);
  if (Number.isNaN(d.getTime())) return String(fecha);
  return d.toLocaleString("es-MX", {
    dateStyle: "short",
    timeStyle: "short",
  });
}

export default function Administracion() {
  const { toast } = useToast();
const navigate = useNavigate();
  // Sección existente de usuarios (todavía mock).
  const [q, setQ] = useState("");
  const [lista, setLista] = useState(usuarios);
  const [nuevo, setNuevo] = useState({ nombre: "", email: "", rol: "Capturador", unidad: "" });
  const [open, setOpen] = useState(false);

  // Pacientes dados de baja (API real).
  const [bajas, setBajas] = useState([]);
  const [bajasLoading, setBajasLoading] = useState(false);
  const [bajasError, setBajasError] = useState("");
  const [qBajas, setQBajas] = useState("");
  const [reactivarPaciente, setReactivarPaciente] = useState(null);
  const [reactivando, setReactivando] = useState(false);

  const filtrados = lista.filter(
    (u) => !q || u.nombre.toLowerCase().includes(q.toLowerCase()) || u.email.includes(q)
  );

  const bajasFiltradas = useMemo(() => {
    const termino = qBajas.trim().toLowerCase();
    if (!termino) return bajas;

    return bajas.filter((p) => {
      const texto = [
        nombrePaciente(p),
        value(p, "municipioResidencia", "municipio_residencia"),
        value(p, "estadoResidencia", "estado_residencia"),
        value(p, "tipoCartilla", "tipo_cartilla"),
        value(p, "motivoBaja", "motivo_baja"),
        value(p, "usuarioBaja", "usuario_baja"),
      ].filter(Boolean).join(" ").toLowerCase();

      return texto.includes(termino);
    });
  }, [bajas, qBajas]);

  const toggleActivo = (id) => {
    setLista(lista.map((u) => (u.id === id ? { ...u, activo: !u.activo } : u)));
  };

  const crear = () => {
    if (!nuevo.nombre || !nuevo.email) {
      toast({ title: "Nombre y correo son obligatorios", variant: "destructive" });
      return;
    }
    const id = "us" + Date.now();
    setLista([
      ...lista,
      { id, username: nuevo.email.split("@")[0], nombre: nuevo.nombre, email: nuevo.email, rol: nuevo.rol, unidad: nuevo.unidad || "—", activo: true, ultimo_acceso: "—" },
    ]);
    toast({ title: "Usuario creado", description: "Se envió invitación por correo." });
    setNuevo({ nombre: "", email: "", rol: "Capturador", unidad: "" });
    setOpen(false);
  };

  async function cargarBajas() {
    setBajasLoading(true);
    setBajasError("");
    try {
      const result = await apiClient.get("/pacientes/bajas");
      setBajas(Array.isArray(result) ? result : []);
    } catch (err) {
      setBajasError(err.message || "No fue posible cargar los pacientes dados de baja.");
    } finally {
      setBajasLoading(false);
    }
  }

  useEffect(() => {
    cargarBajas();
  }, []);

  async function confirmarReactivacion() {
    if (!reactivarPaciente) return;

    const pacienteId = value(reactivarPaciente, "id", "id");
    if (!pacienteId) return;

    setReactivando(true);
    try {
      await apiClient.post(`/pacientes/${encodeURIComponent(pacienteId)}/reactivar`, {});
      setBajas((actuales) =>
        actuales.filter((p) => String(value(p, "id", "id")) !== String(pacienteId))
      );
      toast({
        title: "Paciente reactivado",
        description: `${nombrePaciente(reactivarPaciente)} volvió al padrón activo.`,
      });
      setReactivarPaciente(null);
    } catch (err) {
      toast({
        title: "No fue posible reactivar al paciente",
        description: err.message || "La API rechazó la reactivación.",
        variant: "destructive",
      });
    } finally {
      setReactivando(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-heading font-semibold">Administración</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Usuarios, roles, permisos y recuperación de pacientes
          </p>
        </div>
        <Button onClick={() => setOpen(!open)}>
          <UserPlus className="w-4 h-4 mr-2" /> Nuevo usuario
        </Button>
      </div>

      {open && (
        <Card>
          <CardHeader><CardTitle className="text-base">Crear usuario</CardTitle></CardHeader>
          <CardContent className="grid sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="space-y-1.5">
              <Label>Nombre completo</Label>
              <Input value={nuevo.nombre} onChange={(e) => setNuevo({ ...nuevo, nombre: e.target.value })} />
            </div>
            <div className="space-y-1.5">
              <Label>Correo</Label>
              <Input type="email" value={nuevo.email} onChange={(e) => setNuevo({ ...nuevo, email: e.target.value })} />
            </div>
            <div className="space-y-1.5">
              <Label>Rol</Label>
              <Select value={nuevo.rol} onValueChange={(v) => setNuevo({ ...nuevo, rol: v })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {roles.map((r) => (
                    <SelectItem key={r.nombre} value={r.nombre}>{r.nombre}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Unidad / Jurisdicción</Label>
              <Input value={nuevo.unidad} onChange={(e) => setNuevo({ ...nuevo, unidad: e.target.value })} />
            </div>
            <div className="sm:col-span-2 lg:col-span-4 flex justify-end gap-2">
              <Button variant="outline" onClick={() => setOpen(false)}>Cancelar</Button>
              <Button onClick={crear}>Crear usuario</Button>
            </div>
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="usuarios">
        <TabsList className="flex flex-wrap h-auto">
          <TabsTrigger value="usuarios">Usuarios</TabsTrigger>
          <TabsTrigger value="roles">Roles y permisos</TabsTrigger>
          <TabsTrigger value="bajas">
            <UserX className="w-4 h-4 mr-2" />
            Pacientes dados de baja
            {bajas.length > 0 && (
              <Badge variant="secondary" className="ml-2">{bajas.length}</Badge>
            )}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="usuarios" className="space-y-4">
          <div className="relative max-w-md">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <Input placeholder="Buscar usuario…" className="pl-9" value={q} onChange={(e) => setQ(e.target.value)} />
          </div>

          <Card>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Usuario</TableHead>
                    <TableHead>Rol</TableHead>
                    <TableHead>Unidad</TableHead>
                    <TableHead>Último acceso</TableHead>
                    <TableHead>Estado</TableHead>
                    <TableHead className="text-right">Acción</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filtrados.map((u) => (
                    <TableRow key={u.id}>
                      <TableCell>
                        <div className="flex items-center gap-3">
                          <Avatar className="w-9 h-9"><AvatarFallback>{iniciales(u.nombre)}</AvatarFallback></Avatar>
                          <div>
                            <p className="font-medium">{u.nombre}</p>
                            <p className="text-xs text-muted-foreground">{u.email}</p>
                          </div>
                        </div>
                      </TableCell>
                      <TableCell><Badge variant="secondary">{u.rol}</Badge></TableCell>
                      <TableCell className="text-muted-foreground">{u.unidad}</TableCell>
                      <TableCell className="text-xs text-muted-foreground">{u.ultimo_acceso}</TableCell>
                      <TableCell>
                        {u.activo ? <Badge className="bg-emerald-100 text-emerald-700">Activo</Badge> : <Badge variant="outline">Inactivo</Badge>}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm" onClick={() => toggleActivo(u.id)}>
                          {u.activo ? "Desactivar" : "Activar"}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="roles" className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            {roles.map((r) => (
              <Card key={r.nombre}>
                <CardHeader>
                  <div className="flex items-center gap-2">
                    <ShieldCheck className="w-4 h-4 text-primary" />
                    <CardTitle className="text-base">{r.nombre}</CardTitle>
                  </div>
                  <CardDescription>{r.descripcion}</CardDescription>
                </CardHeader>
                <CardContent>
                  <p className="text-xs text-muted-foreground mb-2">Permisos</p>
                  <div className="flex flex-wrap gap-1.5">
                    {r.permisos.map((p) => (
                      <Badge key={p} variant="outline" className="capitalize">{p === "*" ? "Todos" : p}</Badge>
                    ))}
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="bajas" className="space-y-4">
          <div className="flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between">
            <div className="relative max-w-md w-full">
              <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder="Buscar paciente, municipio, motivo…"
                className="pl-9"
                value={qBajas}
                onChange={(e) => setQBajas(e.target.value)}
              />
            </div>
            <Button variant="outline" onClick={cargarBajas} disabled={bajasLoading}>
              {bajasLoading
                ? <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                : <RotateCcw className="w-4 h-4 mr-2" />}
              Actualizar
            </Button>
          </div>

          {bajasError && (
            <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">
              {bajasError}
            </div>
          )}

          <Card>
            <CardHeader>
              <CardTitle>Pacientes dados de baja</CardTitle>
              <CardDescription>
                La reactivación conserva el mismo expediente y genera un evento REACTIVACION en la auditoría.
              </CardDescription>
            </CardHeader>
            <CardContent className="p-0 overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Paciente</TableHead>
                    <TableHead>Residencia</TableHead>
                    <TableHead>Cartilla</TableHead>
                    <TableHead>Fecha de baja</TableHead>
                    <TableHead>Motivo / usuario</TableHead>
                    <TableHead className="text-right">Acción</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {bajasLoading && bajas.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center py-10 text-muted-foreground">
                        <Loader2 className="w-5 h-5 mx-auto mb-2 animate-spin" />
                        Cargando pacientes dados de baja…
                      </TableCell>
                    </TableRow>
                  )}

                  {!bajasLoading && bajasFiltradas.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center py-10 text-muted-foreground">
                        No hay pacientes dados de baja que coincidan con la búsqueda.
                      </TableCell>
                    </TableRow>
                  )}

                  {bajasFiltradas.map((p) => {
                    const pacienteId = value(p, "id", "id");
                    const municipio = value(p, "municipioResidencia", "municipio_residencia") || "—";
                    const estado = value(p, "estadoResidencia", "estado_residencia") || "—";
                    const usuarioBaja = value(p, "usuarioBaja", "usuario_baja") || "—";
                    const usuarioRol = value(p, "usuarioRol", "usuario_rol") || "";
                    return (
                      <TableRow key={pacienteId}>
                        <TableCell>
                          <div>
                            <p className="font-medium">{nombrePaciente(p) || "Paciente"}</p>
                            <p className="text-xs text-muted-foreground">
                              {value(p, "sexo", "sexo") === "F" ? "Femenino" :
                               value(p, "sexo", "sexo") === "M" ? "Masculino" : "—"}
                            </p>
                          </div>
                        </TableCell>
                        <TableCell>
                          <p>{municipio}</p>
                          <p className="text-xs text-muted-foreground">{estado}</p>
                        </TableCell>
                        <TableCell>{value(p, "tipoCartilla", "tipo_cartilla") || "—"}</TableCell>
                        <TableCell className="text-sm">
                          {fechaHoraMX(value(p, "fechaBaja", "fecha_baja"))}
                        </TableCell>
                        <TableCell className="max-w-xs">
                          <p className="text-sm">{value(p, "motivoBaja", "motivo_baja") || "—"}</p>
                          <p className="text-xs text-muted-foreground mt-1">
                            {usuarioBaja}{usuarioRol ? ` · ${usuarioRol}` : ""}
                          </p>
                        </TableCell>
                       <TableCell className="text-right">
  <div className="flex justify-end gap-2">
    <Button
      variant="outline"
      size="sm"
      onClick={() =>
        navigate(
          `/administracion/pacientes-inactivos/${encodeURIComponent(pacienteId)}`
        )
      }
    >
      <Eye className="w-4 h-4 mr-2" />
      Ver expediente
    </Button>

    <Button
      variant="outline"
      size="sm"
      onClick={() => setReactivarPaciente(p)}
    >
      <RotateCcw className="w-4 h-4 mr-2" />
      Reactivar
    </Button>
  </div>
</TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <Dialog
        open={Boolean(reactivarPaciente)}
        onOpenChange={(abierto) => {
          if (!abierto && !reactivando) setReactivarPaciente(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reactivar paciente</DialogTitle>
          </DialogHeader>

          <div className="space-y-3 text-sm">
            <p>
              Se reactivará a{" "}
              <strong>{reactivarPaciente ? nombrePaciente(reactivarPaciente) : "este paciente"}</strong>.
            </p>
            <p className="text-muted-foreground">
              El paciente volverá al padrón activo conservando su mismo expediente e historial.
              La operación quedará registrada como REACTIVACION en la auditoría.
            </p>
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              disabled={reactivando}
              onClick={() => setReactivarPaciente(null)}
            >
              Cancelar
            </Button>
            <Button
              type="button"
              disabled={reactivando}
              onClick={confirmarReactivacion}
            >
              {reactivando && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
              Confirmar reactivación
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
