import React, { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  Table, TableHeader, TableBody, TableHead, TableRow, TableCell,
} from "@/components/ui/table";
import { Search, ChevronLeft, ChevronRight, Loader2, AlertTriangle, Plus } from "lucide-react";
import { apiClient } from "@/api/apiClient";
import DomicilioAlta, { DOMICILIO_INICIAL, domicilioValido, payloadDomicilio } from "@/components/DomicilioAlta";

const PAGE_SIZE = 25;
const FORM_INICIAL = {
  curp: "", nombre: "", primerApellido: "", segundoApellido: "", fechaNacimiento: "",
  sexo: "", entidadNacimientoClave: "", estadoResidenciaClave: "", municipioResidenciaId: "",
  localidad: "", tipoCartillaId: "", direccion: "", tutorNombre: "", tutorCurp: "", tutorTelefono: "",
  pertencePuebloIndigena: false, esMigrante: false,
};

const value = (obj, camel, snake) => {
  if (!obj) return undefined;

  const pascal =
    camel.charAt(0).toUpperCase() + camel.slice(1);

  return obj[camel]
    ?? obj[pascal]
    ?? obj[snake];
};
const nombreCompleto = (p) => [
  value(p, "nombre", "nombre"), value(p, "primerApellido", "primer_apellido"), value(p, "segundoApellido", "segundo_apellido"),
].filter(Boolean).join(" ");

function edad(fecha) {
  if (!fecha) return "—";
  const nacimiento = new Date(fecha);
  if (Number.isNaN(nacimiento.getTime())) return "—";
  const hoy = new Date();
  let anios = hoy.getFullYear() - nacimiento.getFullYear();
  const m = hoy.getMonth() - nacimiento.getMonth();
  if (m < 0 || (m === 0 && hoy.getDate() < nacimiento.getDate())) anios--;
  if (anios >= 1) return `${anios} años`;
  const meses = Math.max(0, (hoy.getFullYear() - nacimiento.getFullYear()) * 12 + hoy.getMonth() - nacimiento.getMonth() - (hoy.getDate() < nacimiento.getDate() ? 1 : 0));
  return `${meses} meses`;
}

function edadEnAnios(fecha) {
  if (!fecha) return null;
  const nacimiento = new Date(`${fecha}T00:00:00`);
  if (Number.isNaN(nacimiento.getTime())) return null;
  const hoy = new Date();
  let anios = hoy.getFullYear() - nacimiento.getFullYear();
  const m = hoy.getMonth() - nacimiento.getMonth();
  if (m < 0 || (m === 0 && hoy.getDate() < nacimiento.getDate())) anios--;
  return anios;
}

function sexoTexto(sexo) {
  if (sexo === "M") return "Masculino";
  if (sexo === "F") return "Femenino";
  return sexo || "—";
}

export default function Pacientes() {
  const [q, setQ] = useState("");
  const [busqueda, setBusqueda] = useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState({ items: [], total: 0, totalPages: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [reloadKey, setReloadKey] = useState(0);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState(FORM_INICIAL);
  const [estados, setEstados] = useState([]);
  const [municipios, setMunicipios] = useState([]);
  const [tiposCartilla, setTiposCartilla] = useState([]);
  const [catalogosLoading, setCatalogosLoading] = useState(false);
  const [guardando, setGuardando] = useState(false);
  const [formError, setFormError] = useState(null);
  const [success, setSuccess] = useState(null);
  const [domicilioNuevo, setDomicilioNuevo] = useState(DOMICILIO_INICIAL);
  const [activarDomicilio, setActivarDomicilio] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => { setPage(1); setBusqueda(q.trim()); }, 350);
    return () => clearTimeout(timer);
  }, [q]);

  useEffect(() => {
    let cancelled = false;
    async function cargar() {
      setLoading(true); setError(null);
      try {
        const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
        if (busqueda) {
          if (/^[A-Za-z0-9]{18}$/.test(busqueda)) params.set("curp", busqueda.toUpperCase());
          else params.set("nombre", busqueda);
        }
        const result = await apiClient.get(`/pacientes?${params.toString()}`);
        if (!cancelled) setData({
          items: Array.isArray(result?.items) ? result.items : [],
          total: Number(result?.total ?? 0),
          totalPages: Number(result?.totalPages ?? result?.total_pages ?? 0),
        });
      } catch (err) {
        if (!cancelled) setError(err.message || "No fue posible cargar los pacientes.");
      } finally { if (!cancelled) setLoading(false); }
    }
    cargar();
    return () => { cancelled = true; };
  }, [busqueda, page, reloadKey]);

  useEffect(() => {
    if (!dialogOpen || estados.length) return;
    let cancelled = false;
    setCatalogosLoading(true);
    Promise.all([
      apiClient.get("/catalogo/estados"),
      apiClient.get("/catalogo/tipos-cartilla"),
    ])
      .then(([e, c]) => {
        if (!cancelled) {
          setEstados(Array.isArray(e) ? e : []);
          setTiposCartilla(Array.isArray(c) ? c : []);
        }
      })
      .catch((e) => { if (!cancelled) setFormError(e.message || "No fue posible cargar los catálogos."); })
      .finally(() => { if (!cancelled) setCatalogosLoading(false); });
    return () => { cancelled = true; };
  }, [dialogOpen, estados.length, tiposCartilla.length]);

  useEffect(() => {
    if (!dialogOpen || !form.estadoResidenciaClave) { setMunicipios([]); return; }
    let cancelled = false;
    setCatalogosLoading(true);
    apiClient.get(`/catalogo/municipios?estadoClave=${encodeURIComponent(form.estadoResidenciaClave)}`)
      .then((r) => { if (!cancelled) setMunicipios(Array.isArray(r) ? r : []); })
      .catch((e) => { if (!cancelled) setFormError(e.message || "No fue posible cargar los municipios."); })
      .finally(() => { if (!cancelled) setCatalogosLoading(false); });
    return () => { cancelled = true; };
  }, [dialogOpen, form.estadoResidenciaClave]);

  useEffect(() => {
    if (!dialogOpen || !form.fechaNacimiento || !form.sexo || !tiposCartilla.length) return;
    const anios = edadEnAnios(form.fechaNacimiento);
    if (anios === null || anios < 0) return;

    const sugerida = tiposCartilla.find((c) => {
      const minima = Number(value(c, "edadMinima", "edad_minima"));
      const maximaRaw = value(c, "edadMaxima", "edad_maxima");
      const maxima = maximaRaw == null ? null : Number(maximaRaw);
      const sexoCartilla = value(c, "sexo", "sexo");
      return anios >= minima && (maxima === null || anios <= maxima)
        && (!sexoCartilla || sexoCartilla === form.sexo);
    });

    if (sugerida) {
      const id = String(value(sugerida, "id", "id"));
      setForm((f) => f.tipoCartillaId === id ? f : ({ ...f, tipoCartillaId: id }));
    }
  }, [dialogOpen, form.fechaNacimiento, form.sexo, tiposCartilla]);

  const pacientes = useMemo(() => data.items, [data.items]);
  const setCampo = (campo, valor) => setForm((f) => ({ ...f, [campo]: valor }));

  function abrirNuevo() {
    setForm(FORM_INICIAL); setDomicilioNuevo(DOMICILIO_INICIAL); setActivarDomicilio(false); setFormError(null); setSuccess(null); setDialogOpen(true);
  }

  async function guardarPaciente(e) {
    e.preventDefault(); setFormError(null); setSuccess(null);
    if (!form.curp || form.curp.trim().length !== 18) return setFormError("La CURP debe contener 18 caracteres.");
    if (!form.nombre.trim() || !form.primerApellido.trim()) return setFormError("Nombre y primer apellido son obligatorios.");
    if (!form.fechaNacimiento || !form.sexo || !form.entidadNacimientoClave || !form.municipioResidenciaId || !form.tipoCartillaId) return setFormError("Completa fecha de nacimiento, sexo, entidad de nacimiento, municipio de residencia y tipo de cartilla.");

    if (activarDomicilio && !domicilioValido(domicilioNuevo))
      return setFormError("Para registrar el domicilio estructurado debes seleccionar una ubicación válida en el mapa.");
    setGuardando(true);
    try {
      const pacienteCreado = await apiClient.post("/pacientes", {
        curp: form.curp.trim().toUpperCase(), nombre: form.nombre.trim(), primerApellido: form.primerApellido.trim(),
        segundoApellido: form.segundoApellido.trim() || null, fechaNacimiento: form.fechaNacimiento, sexo: form.sexo,
        entidadNacimientoClave: form.entidadNacimientoClave, municipioResidenciaId: Number(form.municipioResidenciaId),
        localidad: form.localidad.trim() || null, tipoCartillaId: Number(form.tipoCartillaId),
        direccion: form.direccion.trim() || null, tutorNombre: form.tutorNombre.trim() || null,
        tutorCurp: form.tutorCurp.trim().toUpperCase() || null, tutorTelefono: form.tutorTelefono.trim() || null,
        pertencePuebloIndigena: form.pertencePuebloIndigena, esMigrante: form.esMigrante,
      });
      // El alta nominal ya se completó. Un fallo posterior NO debe reintentar crear al paciente.
      let mensaje = "Paciente registrado correctamente en SNCEV.";
      if (activarDomicilio) {
        const idCreado = typeof pacienteCreado === "string" ? pacienteCreado :
          (pacienteCreado?.id ?? pacienteCreado?.Id ?? pacienteCreado?.pacienteId ?? pacienteCreado?.PacienteId ?? pacienteCreado?.paciente?.id);
        if (!idCreado) {
          mensaje += " ATENCIÓN: la API no devolvió el ID del paciente; el domicilio estructurado no se registró. Agrégalo desde su expediente.";
        } else {
          try {
            await apiClient.post(`/pacientes/${encodeURIComponent(idCreado)}/domicilios`, payloadDomicilio(domicilioNuevo));
            mensaje += " Domicilio georreferenciado registrado.";
          } catch (errorDomicilio) {
            mensaje += ` ATENCIÓN: no se pudo guardar el domicilio (${errorDomicilio.message}). El paciente SÍ fue creado; agrégalo desde su expediente, sin repetir el alta.`;
          }
        }
      }
      setDialogOpen(false); setSuccess(mensaje); setPage(1); setReloadKey((x) => x + 1);
    } catch (err) { setFormError(err.message || "No fue posible registrar el paciente."); }
    finally { setGuardando(false); }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-heading font-semibold">Pacientes</h1>
          <p className="text-muted-foreground text-sm mt-1">{loading ? "Consultando SNCEV…" : `${data.total} pacientes registrados`}</p>
        </div>
        <Button onClick={abrirNuevo}><Plus className="w-4 h-4 mr-2" />Nuevo paciente</Button>
      </div>

      {success && <div className="rounded-md border border-emerald-300 bg-emerald-50 p-4 text-sm text-emerald-800">{success}</div>}

      <Card><CardContent className="p-4">
        <div className="relative"><Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
          <Input placeholder="Buscar por nombre, apellidos o CURP completa…" className="pl-9" value={q} onChange={(e) => setQ(e.target.value)} />
        </div>
        <p className="text-xs text-muted-foreground mt-2">Datos nominales obtenidos de SNCEV.Api.</p>
      </CardContent></Card>

      {error && <div className="rounded-md border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive flex gap-2 items-start"><AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" /><span>{error}</span></div>}

      <Card><CardContent className="p-0"><Table>
        <TableHeader><TableRow><TableHead>Paciente</TableHead><TableHead>CURP</TableHead><TableHead>Edad</TableHead><TableHead>Sexo</TableHead><TableHead className="text-right">Acción</TableHead></TableRow></TableHeader>
        <TableBody>
          {loading && <TableRow><TableCell colSpan={5} className="text-center text-muted-foreground py-10"><Loader2 className="w-5 h-5 animate-spin inline mr-2" /> Cargando pacientes…</TableCell></TableRow>}
          {!loading && pacientes.map((p) => {
            const id = value(p, "id", "id"); const fechaNacimiento = value(p, "fechaNacimiento", "fecha_nacimiento");
            return <TableRow key={id}><TableCell className="font-medium">{nombreCompleto(p) || "—"}</TableCell><TableCell className="font-mono text-xs">{value(p, "curp", "curp") || "—"}</TableCell><TableCell>{edad(fechaNacimiento)}</TableCell><TableCell>{sexoTexto(value(p, "sexo", "sexo"))}</TableCell><TableCell className="text-right"><Link to={`/pacientes/${id}`}><Button variant="ghost" size="sm">Ver detalle</Button></Link></TableCell></TableRow>;
          })}
          {!loading && !error && pacientes.length === 0 && <TableRow><TableCell colSpan={5} className="text-center text-muted-foreground py-10">No se encontraron pacientes</TableCell></TableRow>}
        </TableBody>
      </Table>
      {!loading && data.totalPages > 1 && <div className="flex items-center justify-between gap-3 p-4 border-t"><p className="text-sm text-muted-foreground">Página {page} de {data.totalPages}</p><div className="flex gap-2"><Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}><ChevronLeft className="w-4 h-4 mr-1" />Anterior</Button><Button variant="outline" size="sm" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>Siguiente<ChevronRight className="w-4 h-4 ml-1" /></Button></div></div>}
      </CardContent></Card>

      <Dialog open={dialogOpen} onOpenChange={(open) => { if (!guardando) setDialogOpen(open); }}>
        <DialogContent className="max-w-5xl max-h-[90vh] overflow-y-auto">
          <DialogHeader><DialogTitle>Nuevo paciente</DialogTitle><DialogDescription>Alta nominal en la base local SNCEV. Los campos con * son obligatorios.</DialogDescription></DialogHeader>
          <form onSubmit={guardarPaciente} className="space-y-5">
            {formError && <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">{formError}</div>}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-1.5 md:col-span-2"><Label>CURP *</Label><Input maxLength={18} value={form.curp} onChange={(e) => setCampo("curp", e.target.value.toUpperCase())} placeholder="18 caracteres" /></div>
              <div className="space-y-1.5"><Label>Nombre(s) *</Label><Input maxLength={100} value={form.nombre} onChange={(e) => setCampo("nombre", e.target.value)} /></div>
              <div className="space-y-1.5"><Label>Primer apellido *</Label><Input maxLength={100} value={form.primerApellido} onChange={(e) => setCampo("primerApellido", e.target.value)} /></div>
              <div className="space-y-1.5"><Label>Segundo apellido</Label><Input maxLength={100} value={form.segundoApellido} onChange={(e) => setCampo("segundoApellido", e.target.value)} /></div>
              <div className="space-y-1.5"><Label>Fecha de nacimiento *</Label><Input type="date" max={new Date().toISOString().slice(0,10)} value={form.fechaNacimiento} onChange={(e) => setCampo("fechaNacimiento", e.target.value)} /></div>
              <div className="space-y-1.5"><Label>Sexo *</Label><Select value={form.sexo} onValueChange={(v) => setCampo("sexo", v)}><SelectTrigger><SelectValue placeholder="Selecciona" /></SelectTrigger><SelectContent><SelectItem value="F">Femenino</SelectItem><SelectItem value="M">Masculino</SelectItem></SelectContent></Select></div>
              <div className="space-y-1.5"><Label>Entidad de nacimiento *</Label><Select value={form.entidadNacimientoClave} onValueChange={(v) => setCampo("entidadNacimientoClave", v)} disabled={catalogosLoading}><SelectTrigger><SelectValue placeholder="Selecciona entidad" /></SelectTrigger><SelectContent>{estados.map((e) => <SelectItem key={value(e,"claveInegi","clave_inegi")} value={String(value(e,"claveInegi","clave_inegi"))}>{value(e,"nombre","nombre")}</SelectItem>)}</SelectContent></Select></div>
              <div className="space-y-1.5"><Label>Estado de residencia *</Label><Select value={form.estadoResidenciaClave} onValueChange={(v) => setForm((f) => ({...f, estadoResidenciaClave:v, municipioResidenciaId:""}))} disabled={catalogosLoading}><SelectTrigger><SelectValue placeholder="Selecciona estado" /></SelectTrigger><SelectContent>{estados.map((e) => <SelectItem key={value(e,"claveInegi","clave_inegi")} value={String(value(e,"claveInegi","clave_inegi"))}>{value(e,"nombre","nombre")}</SelectItem>)}</SelectContent></Select></div>
              <div className="space-y-1.5"><Label>Municipio de residencia *</Label><Select value={form.municipioResidenciaId} onValueChange={(v) => setCampo("municipioResidenciaId", v)} disabled={!form.estadoResidenciaClave || catalogosLoading}><SelectTrigger><SelectValue placeholder={form.estadoResidenciaClave ? "Selecciona municipio" : "Primero selecciona estado"} /></SelectTrigger><SelectContent>{municipios.map((m) => <SelectItem key={value(m,"id","id")} value={String(value(m,"id","id"))}>{value(m,"nombre","nombre")}</SelectItem>)}</SelectContent></Select></div>
              <div className="space-y-1.5"><Label>Localidad</Label><Input maxLength={150} value={form.localidad} onChange={(e) => setCampo("localidad", e.target.value)} placeholder="Captura la localidad" /></div>
              <div className="space-y-1.5"><Label>Tipo de cartilla *</Label><Select value={form.tipoCartillaId} onValueChange={(v) => setCampo("tipoCartillaId", v)} disabled={catalogosLoading}><SelectTrigger><SelectValue placeholder="Selecciona cartilla" /></SelectTrigger><SelectContent>{tiposCartilla.map((c) => <SelectItem key={value(c,"id","id")} value={String(value(c,"id","id"))}>{value(c,"nombre","nombre")}</SelectItem>)}</SelectContent></Select><p className="text-xs text-muted-foreground">Se sugiere automáticamente según edad y sexo; puedes cambiarla antes de guardar.</p></div>
              <div className="space-y-1.5 md:col-span-2"><Label>Dirección</Label><Input maxLength={255} value={form.direccion} onChange={(e) => setCampo("direccion", e.target.value)} /></div>
              <div className="space-y-1.5"><Label>Nombre del tutor</Label><Input maxLength={200} value={form.tutorNombre} onChange={(e) => setCampo("tutorNombre", e.target.value)} /></div>
              <div className="space-y-1.5"><Label>CURP del tutor</Label><Input maxLength={18} value={form.tutorCurp} onChange={(e) => setCampo("tutorCurp", e.target.value.toUpperCase())} /></div>
              <div className="space-y-1.5"><Label>Teléfono del tutor</Label><Input maxLength={20} value={form.tutorTelefono} onChange={(e) => setCampo("tutorTelefono", e.target.value)} /></div>
              <div className="flex flex-col justify-end gap-3 pb-1"><label className="flex items-center gap-2 text-sm"><Checkbox checked={form.pertencePuebloIndigena} onCheckedChange={(v) => setCampo("pertencePuebloIndigena", v === true)} />Pertenece a pueblo indígena</label><label className="flex items-center gap-2 text-sm"><Checkbox checked={form.esMigrante} onCheckedChange={(v) => setCampo("esMigrante", v === true)} />Es migrante</label></div>
            </div>
            <div className="space-y-3">
              <label className="flex items-center gap-2 text-sm font-medium">
                <Checkbox checked={activarDomicilio} onCheckedChange={v => setActivarDomicilio(v === true)} disabled={guardando}/>
                Registrar también domicilio georreferenciado
              </label>
              {activarDomicilio && <DomicilioAlta domicilio={domicilioNuevo} onChange={setDomicilioNuevo} disabled={guardando}/>}
            </div>
            <DialogFooter><Button type="button" variant="outline" disabled={guardando} onClick={() => setDialogOpen(false)}>Cancelar</Button><Button type="submit" disabled={guardando || catalogosLoading}>{guardando && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}Guardar paciente</Button></DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
