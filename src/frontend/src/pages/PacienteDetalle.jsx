import React, { useEffect, useMemo, useState } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import {
  Table, TableHeader, TableBody, TableHead, TableRow, TableCell,
} from "@/components/ui/table";
import { ArrowLeft, Syringe, AlertTriangle, Loader2, Pencil, Trash2, MapPin, History } from "lucide-react";
import { apiClient } from "@/api/apiClient";
import GeocodificadorDireccion from "@/components/GeocodificadorDireccion";
import { MapContainer, TileLayer, Marker, GeoJSON, Popup, useMap, useMapEvents } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";

const value = (obj, camel, snake) => {
  if (!obj) return null;
  const pascal = camel.charAt(0).toUpperCase() + camel.slice(1);
  return obj[camel] ?? obj[pascal] ?? obj[snake] ?? null;
};
const nombreCompleto = (p) => [
  value(p, "nombre", "nombre"),
  value(p, "primerApellido", "primer_apellido"),
  value(p, "segundoApellido", "segundo_apellido"),
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
  const meses = Math.max(0,
    (hoy.getFullYear() - nacimiento.getFullYear()) * 12 +
    hoy.getMonth() - nacimiento.getMonth() -
    (hoy.getDate() < nacimiento.getDate() ? 1 : 0));
  return `${meses} meses`;
}

function fechaMX(fecha) {
  if (!fecha) return "—";
  const d = new Date(fecha);
  return Number.isNaN(d.getTime()) ? String(fecha) : d.toLocaleDateString("es-MX");
}

function sexoTexto(sexo) {
  if (sexo === "M") return "Masculino";
  if (sexo === "F") return "Femenino";
  return sexo || "—";
}

function vacunaNombre(v) {
  return value(v, "vacunaNombre", "vacuna_nombre") ||
    value(v, "nombreGenerico", "nombre_generico") ||
    value(v, "claveCensia", "clave_censia") ||
    "Vacuna";
}

function textoCatalogo(v) {
  if (v == null) return null;
  if (typeof v === "string" || typeof v === "number") return String(v);
  if (typeof v === "object") {
    return value(v, "nombre", "nombre")
      || value(v, "descripcion", "descripcion")
      || value(v, "clave", "clave")
      || null;
  }
  return String(v);
}


const marcadorDomicilio = L.divIcon({
  className: "",
  html: `<div style="
    width:30px;height:30px;border-radius:50% 50% 50% 0;
    background:#111827;border:3px solid white;
    box-shadow:0 2px 8px rgba(0,0,0,.35);
    transform:rotate(-45deg);
  "><div style="
    width:8px;height:8px;background:white;border-radius:50%;
    margin:8px 0 0 8px;
  "></div></div>`,
  iconSize: [30, 30],
  iconAnchor: [15, 30],
});

const marcadorClues = L.divIcon({
  className: "",
  html: `<div style="
    min-width:28px;height:28px;padding:0 6px;border-radius:14px;
    background:white;border:2px solid #111827;color:#111827;
    box-shadow:0 2px 7px rgba(0,0,0,.28);
    display:flex;align-items:center;justify-content:center;
    font-size:12px;font-weight:700;
  ">+</div>`,
  iconSize: [28, 28],
  iconAnchor: [14, 14],
  popupAnchor: [0, -16],
});

function cvegeoResultado(resultado) {
  const ageb =
    value(resultado, "ageb", "ageb") ||
    value(resultado, "agebResuelta", "ageb_resuelta");

  return (
    value(resultado, "cvegeo", "cvegeo") ||
    value(ageb, "cvegeo", "cvegeo") ||
    null
  );
}

function cveAgebResultado(resultado) {
  const ageb =
    value(resultado, "ageb", "ageb") ||
    value(resultado, "agebResuelta", "ageb_resuelta");

  return (
    value(resultado, "cveAgeb", "cve_ageb") ||
    value(ageb, "cveAgeb", "cve_ageb") ||
    null
  );
}

function cluesResultado(resultado) {
  const unidades =
    value(resultado, "cluesCercanas", "clues_cercanas") ||
    value(resultado, "unidadesCercanas", "unidades_cercanas") ||
    value(resultado, "establecimientosCercanos", "establecimientos_cercanos") ||
    [];

  return Array.isArray(unidades) ? unidades : [];
}

function AjustarMapaAGeB({ geoJson, latitud, longitud }) {
  const map = useMap();

  useEffect(() => {
    if (geoJson) {
      try {
        const capa = L.geoJSON(geoJson);
        const bounds = capa.getBounds();
        if (bounds.isValid()) {
          map.fitBounds(bounds, { padding: [24, 24], maxZoom: 17 });
          return;
        }
      } catch {
        // Si el GeoJSON no puede calcular bounds, usamos la coordenada.
      }
    }

    if (Number.isFinite(latitud) && Number.isFinite(longitud)) {
      map.setView([latitud, longitud], 16);
    }
  }, [map, geoJson, latitud, longitud]);

  return null;
}

function SelectorUbicacion({ latitud, longitud, onChange }) {
  useMapEvents({
    click(e) {
      onChange(e.latlng.lat, e.latlng.lng);
    },
  });

  if (!Number.isFinite(latitud) || !Number.isFinite(longitud)) return null;

  return (
    <Marker
      position={[latitud, longitud]}
      icon={marcadorDomicilio}
      draggable
      eventHandlers={{
        dragend(e) {
          const pos = e.target.getLatLng();
          onChange(pos.lat, pos.lng);
        },
      }}
    />
  );
}

export default function PacienteDetalle() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [expediente, setExpediente] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notFound, setNotFound] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [form, setForm] = useState(null);
  const [estados, setEstados] = useState([]);
 
  const [tiposCartilla, setTiposCartilla] = useState([]);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [bajaOpen, setBajaOpen] = useState(false);
  const [motivoBaja, setMotivoBaja] = useState("");
  const [bajaError, setBajaError] = useState("");
  const [deleting, setDeleting] = useState(false);
  const [domicilioOpen, setDomicilioOpen] = useState(false);
  const [historialOpen, setHistorialOpen] = useState(false);
  const [historialDomicilios, setHistorialDomicilios] = useState([]);
  const [domicilioSaving, setDomicilioSaving] = useState(false);
  const [domicilioError, setDomicilioError] = useState("");
  const [domicilioForm, setDomicilioForm] = useState(null);
  const [geoResolviendo, setGeoResolviendo] = useState(false);
  const [geoResultado, setGeoResultado] = useState(null);
  const [geoError, setGeoError] = useState("");
  const [agebGeoJson, setAgebGeoJson] = useState(null);
  const [agebGeoJsonError, setAgebGeoJsonError] = useState("");
  const [residenciaAgebGeoJson, setResidenciaAgebGeoJson] = useState(null);

  const usuarioActual = (() => {
    try { return JSON.parse(localStorage.getItem("sncev_user") || "null"); }
    catch { return null; }
  })();
  const rolesActuales = Array.isArray(usuarioActual?.roles)
  ? usuarioActual.roles
  : [];

const esAdministrador = rolesActuales.some(
  rol => String(rol).trim().toLowerCase() === "administrador"
);
 

  useEffect(() => {
    let cancelled = false;

    async function cargar() {
      setLoading(true);
      setError("");
      setNotFound(false);
      try {
        // /expediente ya contiene el paciente y su historial de vacunas aplicadas,
        // por lo que evitamos hacer dos solicitudes para la misma pantalla.
        const result = await apiClient.get(`/pacientes/${encodeURIComponent(id)}/expediente`);
        if (!cancelled) setExpediente(result);
      } catch (err) {
        if (!cancelled) {
          if (err.status === 404) setNotFound(true);
          else setError(err.message || "No fue posible cargar el expediente del paciente.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    cargar();
    return () => { cancelled = true; };
  }, [id]);

  const p = expediente?.paciente ?? null;

const domicilio = expediente?.domicilioVigente
  ?? expediente?.domicilio_vigente
  ?? null;

  // Carga el polígono del AGEB para el mapa de solo lectura
  // de la tarjeta "Residencia y cartilla".
  useEffect(() => {
    let cancelled = false;

    async function cargarAgebResidencia() {
      const cvegeo =
        value(domicilio, "cvegeo", "cvegeo") ||
        value(domicilio, "agebClave", "ageb_clave");

      if (!domicilio || !cvegeo) {
        if (!cancelled) setResidenciaAgebGeoJson(null);
        return;
      }

      try {
        const feature = await apiClient.get(
          `/geografia/agebs/${encodeURIComponent(cvegeo)}`
        );
        if (!cancelled) setResidenciaAgebGeoJson(feature);
      } catch {
        if (!cancelled) setResidenciaAgebGeoJson(null);
      }
    }

    cargarAgebResidencia();
    return () => { cancelled = true; };
  }, [domicilio]);

const vacunas = useMemo(() => {
  const items =
    expediente?.vacunasAplicadas ??
    expediente?.vacunas_aplicadas ??
    [];

  return Array.isArray(items) ? items : [];
}, [expediente]);

  async function recargar() {
    const result = await apiClient.get(`/pacientes/${encodeURIComponent(id)}/expediente`);
    setExpediente(result);
  }

async function abrirEdicion() {
  const paciente = expediente?.paciente;
  if (!paciente) return;

  setFormError("");

  const [est, cart] = await Promise.all([
    apiClient.get("/catalogo/estados"),
    apiClient.get("/catalogo/tipos-cartilla")
  ]);

  setEstados(Array.isArray(est) ? est : []);
  setTiposCartilla(Array.isArray(cart) ? cart : []);

  setForm({
    curp: value(paciente, "curp", "curp") || "",
    nombre: value(paciente, "nombre", "nombre") || "",
    primerApellido:
      value(paciente, "primerApellido", "primer_apellido") || "",
    segundoApellido:
      value(paciente, "segundoApellido", "segundo_apellido") || "",

    fechaNacimiento: String(
      value(paciente, "fechaNacimiento", "fecha_nacimiento") || ""
    ).slice(0, 10),

    sexo: value(paciente, "sexo", "sexo") || "",

    entidadNacimientoClave:
      value(
        paciente,
        "entidadNacimientoClave",
        "entidad_nacimiento_clave"
      ) || "",

    tipoCartillaId: String(
      value(paciente, "tipoCartillaId", "tipo_cartilla_id") || ""
    ),

    tutorNombre:
      value(paciente, "tutorNombre", "tutor_nombre") || "",

    tutorCurp:
      value(paciente, "tutorCurp", "tutor_curp") || "",

    tutorTelefono:
      value(paciente, "tutorTelefono", "tutor_telefono") || "",

    pertencePuebloIndigena: Boolean(
      value(
        paciente,
        "pertencePuebloIndigena",
        "pertence_pueblo_indigena"
      )
    ),

    esMigrante: Boolean(
      value(paciente, "esMigrante", "es_migrante")
    )
  });

  setEditOpen(true);
}



 async function guardarEdicion(e) {
  e.preventDefault();

  setFormError("");
  setSaving(true);

  try {
    await apiClient.put(
      `/pacientes/${encodeURIComponent(id)}`,
      {
        curp: form.curp.trim().toUpperCase(),

        nombre: form.nombre.trim(),

        primerApellido:
          form.primerApellido.trim(),

        segundoApellido:
          form.segundoApellido.trim() || null,

        fechaNacimiento:
          form.fechaNacimiento,

        sexo:
          form.sexo,

        entidadNacimientoClave:
          form.entidadNacimientoClave,

        tipoCartillaId:
          Number(form.tipoCartillaId),

        tutorNombre:
          form.tutorNombre.trim() || null,

        tutorCurp:
          form.tutorCurp.trim().toUpperCase() || null,

        tutorTelefono:
          form.tutorTelefono.trim() || null,

        pertencePuebloIndigena:
          form.pertencePuebloIndigena,

        esMigrante:
          form.esMigrante
      }
    );

    await recargar();

    setEditOpen(false);
  }
  catch (err) {
    setFormError(
      err.message ||
      "No fue posible actualizar el paciente."
    );
  }
  finally {
    setSaving(false);
  }
}

  function abrirCambioDomicilio() {
    setDomicilioError("");
    setDomicilioForm({
      localidadClave: value(domicilio, "localidadClave", "localidad_clave") || "",
      localidad: value(domicilio, "localidad", "localidad") || "",
      colonia: value(domicilio, "colonia", "colonia") || "",
      codigoPostal: value(domicilio, "codigoPostal", "codigo_postal") || "",
      calle: value(domicilio, "calle", "calle") || "",
      numeroExterior: value(domicilio, "numeroExterior", "numero_exterior") || "",
      numeroInterior: value(domicilio, "numeroInterior", "numero_interior") || "",
      manzana: value(domicilio, "manzana", "manzana") || "",
      lote: value(domicilio, "lote", "lote") || "",
      direccionTexto: value(domicilio, "direccionTexto", "direccion_texto") || "",
      latitud: value(domicilio, "latitud", "latitud") ?? "",
      longitud: value(domicilio, "longitud", "longitud") ?? "",
      origenGeorreferencia: "MAPA",
      precisionGeorreferencia: "MANUAL",
    });
    setGeoResultado(null);
    setGeoError("");
    setAgebGeoJson(null);
    setAgebGeoJsonError("");
    setDomicilioOpen(true);

    const latRaw = value(domicilio, "latitud", "latitud");
    const lngRaw = value(domicilio, "longitud", "longitud");
    const lat = latRaw == null || latRaw === "" ? NaN : Number(latRaw);
    const lng = lngRaw == null || lngRaw === "" ? NaN : Number(lngRaw);
    if (Number.isFinite(lat) && Number.isFinite(lng)) {
      resolverUbicacion(lat, lng);
    }
  }

  async function resolverUbicacion(latitud, longitud) {
    if (!Number.isFinite(latitud) || !Number.isFinite(longitud)) return;

    setGeoResolviendo(true);
    setGeoError("");

    try {
      const r = await apiClient.get(
        `/geografia/resolver?latitud=${encodeURIComponent(latitud)}&longitud=${encodeURIComponent(longitud)}&limiteClues=5`
      );
      setGeoResultado(r);

      const cvegeo = cvegeoResultado(r);
      if (cvegeo) {
        try {
          const feature = await apiClient.get(
            `/geografia/agebs/${encodeURIComponent(cvegeo)}`
          );
          setAgebGeoJson(feature);
          setAgebGeoJsonError("");
        } catch (agebErr) {
          setAgebGeoJson(null);
          setAgebGeoJsonError(
            agebErr.message || "No fue posible cargar el polígono del AGEB."
          );
        }
      } else {
        setAgebGeoJson(null);
        setAgebGeoJsonError("");
      }
    } catch (err) {
      setGeoResultado(null);
      setAgebGeoJson(null);
      setAgebGeoJsonError("");
      setGeoError(err.message || "No fue posible resolver la ubicación geográfica.");
    } finally {
      setGeoResolviendo(false);
    }
  }

  function cambiarUbicacionMapa(latitud, longitud) {
    const lat = Number(latitud.toFixed(7));
    const lng = Number(longitud.toFixed(7));

    setDomicilioForm((f) => ({
      ...f,
      latitud: lat,
      longitud: lng,
      origenGeorreferencia: "MAPA",
      precisionGeorreferencia: "MANUAL",
    }));

    resolverUbicacion(lat, lng);
  }

  async function guardarDomicilio(e) {
    e.preventDefault();
    setDomicilioError("");
    const latitud = domicilioForm.latitud === "" ? NaN : Number(domicilioForm.latitud);
    const longitud = domicilioForm.longitud === "" ? NaN : Number(domicilioForm.longitud);
    if (!Number.isFinite(latitud) || !Number.isFinite(longitud)) {
      setDomicilioError("Selecciona una ubicación válida en el mapa.");
      return;
    }
    // La cobertura AGEB puede faltar fuera de CDMX.
    // No bloqueamos un domicilio con coordenadas verificadas por esa razón.
    if (geoResolviendo) {
      setDomicilioError("Espera a que termine la consulta geográfica antes de guardar.");
      return;
    }
    setDomicilioSaving(true);
    try {
      await apiClient.post(`/pacientes/${encodeURIComponent(id)}/domicilios`, {
        localidadClave: domicilioForm.localidadClave.trim() || null,
        localidad: domicilioForm.localidad.trim() || null,
        colonia: domicilioForm.colonia.trim() || null,
        codigoPostal: domicilioForm.codigoPostal.trim() || null,
        calle: domicilioForm.calle.trim() || null,
        numeroExterior: domicilioForm.numeroExterior.trim() || null,
        numeroInterior: domicilioForm.numeroInterior.trim() || null,
        manzana: domicilioForm.manzana.trim() || null,
        lote: domicilioForm.lote.trim() || null,
        direccionTexto: domicilioForm.direccionTexto.trim() || null,
        latitud, longitud,
        origenGeorreferencia: domicilioForm.origenGeorreferencia,
        precisionGeorreferencia: domicilioForm.precisionGeorreferencia,
      });
      await recargar();
      setDomicilioOpen(false);
    } catch (err) {
      setDomicilioError(err.message || "No fue posible registrar el nuevo domicilio.");
    } finally {
      setDomicilioSaving(false);
    }
  }

  async function abrirHistorialDomicilios() {
    try {
      const r = await apiClient.get(`/pacientes/${encodeURIComponent(id)}/domicilios`);
      setHistorialDomicilios(Array.isArray(r?.historial) ? r.historial : []);
      setHistorialOpen(true);
    } catch (err) {
      setError(err.message || "No fue posible cargar el historial de domicilios.");
    }
  }

  function abrirBaja() {
    setMotivoBaja("");
    setBajaError("");
    setBajaOpen(true);
  }

  async function confirmarBaja(e) {
    e.preventDefault();
    const motivo = motivoBaja.trim();
    if (!motivo) {
      setBajaError("El motivo de la baja es obligatorio.");
      return;
    }
    setDeleting(true);
    setBajaError("");
    try {
     await apiClient.post(
  `/pacientes/${encodeURIComponent(id)}/baja`,
  { motivo }
);
      setBajaOpen(false);
      navigate("/pacientes", { replace: true });
    } catch (err) {
      setBajaError(err.message || "No fue posible dar de baja al paciente.");
    } finally {
      setDeleting(false);
    }
  }

  if (loading) {
    return (
      <div className="text-center py-20 text-muted-foreground">
        <Loader2 className="w-8 h-8 mx-auto animate-spin" />
        <p className="mt-3">Cargando expediente desde SNCEV.Api…</p>
      </div>
    );
  }

  if (notFound) {
    return (
      <div className="text-center py-20">
        <AlertTriangle className="w-10 h-10 mx-auto text-muted-foreground" />
        <p className="mt-3 text-muted-foreground">Paciente no encontrado</p>
        <Link to="/pacientes"><Button variant="outline" className="mt-4">Volver a pacientes</Button></Link>
      </div>
    );
  }

  if (error || !p) {
    return (
      <div className="text-center py-20">
        <AlertTriangle className="w-10 h-10 mx-auto text-destructive" />
        <p className="mt-3 text-destructive">{error || "El expediente no contiene datos del paciente."}</p>
        <Link to="/pacientes"><Button variant="outline" className="mt-4">Volver a pacientes</Button></Link>
      </div>
    );
  }

  const nombre = nombreCompleto(p);
  const fechaNacimiento = value(p, "fechaNacimiento", "fecha_nacimiento");
  const sexo = value(p, "sexo", "sexo");
  const iniciales = [value(p, "nombre", "nombre"), value(p, "primerApellido", "primer_apellido")]
    .filter(Boolean).map((x) => String(x).charAt(0).toUpperCase()).join("") || "P";

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/pacientes">
          <Button variant="ghost" size="sm"><ArrowLeft className="w-4 h-4 mr-1" /> Pacientes</Button>
        </Link>
      </div>

      <Card>
        <CardContent className="p-6">
          <div className="flex flex-col md:flex-row md:items-center gap-4">
            <div className="w-16 h-16 rounded-full bg-primary text-primary-foreground flex items-center justify-center text-xl font-semibold">
              {iniciales}
            </div>
            <div className="flex-1">
              <h1 className="text-xl font-heading font-semibold">{nombre || "Paciente"}</h1>
              <p className="text-sm text-muted-foreground font-mono">{value(p, "curp", "curp") || "CURP no disponible"}</p>
              <div className="flex flex-wrap gap-2 mt-2">
                <Badge variant="outline">{sexoTexto(sexo)}</Badge>
                <Badge variant="outline">{edad(fechaNacimiento)}</Badge>
                <Badge variant="secondary">{vacunas.length} dosis registradas</Badge>
              </div>
            </div>
            <div className="flex gap-2">
              <Button variant="outline" onClick={abrirEdicion}><Pencil className="w-4 h-4 mr-2" /> Editar datos</Button>
              <Button variant="outline" onClick={abrirCambioDomicilio}><MapPin className="w-4 h-4 mr-2" /> Cambiar domicilio</Button>
              {esAdministrador && (
                <Button variant="destructive" onClick={abrirBaja}>
                  <Trash2 className="w-4 h-4 mr-2" /> Dar de baja
                </Button>
              )}
              <Link to="/vacunacion">
                <Button><Syringe className="w-4 h-4 mr-2" /> Ir a vacunación</Button>
              </Link>
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader><CardDescription>Dosis aplicadas registradas</CardDescription></CardHeader>
          <CardContent><p className="text-3xl font-heading font-semibold text-emerald-600">{vacunas.length}</p></CardContent>
        </Card>
        <Card>
          <CardHeader><CardDescription>Origen de datos</CardDescription></CardHeader>
          <CardContent><p className="font-medium">SNCEV.Api / expediente</p></CardContent>
        </Card>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="space-y-6">
          <Card><CardHeader><CardTitle>Datos personales</CardTitle></CardHeader><CardContent className="space-y-3 text-sm">
            <Row label="CURP" value={value(p,"curp","curp")||"—"} mono /><Row label="Fecha de nacimiento" value={fechaMX(fechaNacimiento)} /><Row label="Edad" value={edad(fechaNacimiento)} /><Row label="Sexo" value={sexoTexto(sexo)} /><Row label="Entidad de nacimiento" value={value(p,"entidadNacimiento","entidad_nacimiento")||"—"} />
          </CardContent></Card>
          <Card>
  <CardHeader>
    <CardTitle>Residencia y cartilla</CardTitle>
    <CardDescription>
      Domicilio vigente del paciente
    </CardDescription>
  </CardHeader>

  <CardContent className="space-y-3 text-sm">

    {domicilio ? (
      <>
        <Row
          label="Estado"
          value={value(domicilio, "estadoNombre", "estado_nombre") || "—"}
        />

        <Row
          label="Municipio / Alcaldía"
          value={value(domicilio, "municipioNombre", "municipio_nombre") || "—"}
        />

        <Row
          label="Localidad"
          value={value(domicilio, "localidad", "localidad") || "—"}
        />

        <Row
          label="Colonia"
          value={value(domicilio, "colonia", "colonia") || "—"}
        />

        <Row
          label="Código postal"
          value={value(domicilio, "codigoPostal", "codigo_postal") || "—"}
        />

        <Row
          label="Dirección"
          value={
            [
              value(domicilio, "calle", "calle"),
              value(domicilio, "numeroExterior", "numero_exterior")
                ? `No. ${value(domicilio, "numeroExterior", "numero_exterior")}`
                : null,
              value(domicilio, "numeroInterior", "numero_interior")
                ? `Int. ${value(domicilio, "numeroInterior", "numero_interior")}`
                : null,
            ].filter(Boolean).join(" ") || "—"
          }
        />

        {value(domicilio, "direccionTexto", "direccion_texto") && (
          <Row
            label="Referencia"
            value={value(domicilio, "direccionTexto", "direccion_texto")}
          />
        )}

        <Row
          label="AGEB"
          value={value(domicilio, "cveAgeb", "cve_ageb") || "—"}
          mono
        />

        <Row
          label="CVEGEO"
          value={value(domicilio, "cvegeo", "cvegeo") || "—"}
          mono
        />
                <Row
          label="Coordenadas"
          value={
            value(domicilio, "latitud", "latitud") != null &&
            value(domicilio, "longitud", "longitud") != null
              ? `${Number(value(domicilio, "latitud", "latitud")).toFixed(6)}, ${Number(
                  value(domicilio, "longitud", "longitud")
                ).toFixed(6)}`
              : "—"
          }
          mono
        />
        <Row
          label="CLUES sugerida"
          value={value(domicilio, "cluesSugerida", "clues_sugerida") || "—"}
          mono
        />

        <Row
          label="Unidad cercana"
          value={
            value(
              domicilio,
              "cluesSugeridaNombre",
              "clues_sugerida_nombre"
            ) || "—"
          }
        />

        {value(domicilio, "latitud", "latitud") != null &&
          value(domicilio, "longitud", "longitud") != null && (
          <div className="pt-2">
            <div className="mb-2 text-sm font-medium">Ubicación del domicilio</div>
            <div className="h-[260px] overflow-hidden rounded-md border">
              <MapContainer
                key={`residencia-${value(domicilio, "id", "id") || "vigente"}`}
                center={[
                  Number(value(domicilio, "latitud", "latitud")),
                  Number(value(domicilio, "longitud", "longitud"))
                ]}
                zoom={16}
                scrollWheelZoom={false}
                dragging={true}
                className="h-full w-full"
              >
                <TileLayer
                  attribution='&copy; OpenStreetMap contributors'
                  url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                />

                {residenciaAgebGeoJson && (
                  <GeoJSON
                    data={residenciaAgebGeoJson}
                    style={() => ({
                      weight: 3,
                      opacity: 0.9,
                      fillOpacity: 0.12,
                    })}
                  />
                )}

                <Marker
                  position={[
                    Number(value(domicilio, "latitud", "latitud")),
                    Number(value(domicilio, "longitud", "longitud"))
                  ]}
                  icon={marcadorDomicilio}
                >
                  <Popup>
                    <div className="space-y-1">
                      <div className="font-semibold">Domicilio vigente</div>
                      <div>
                        {[
                          value(domicilio, "calle", "calle"),
                          value(domicilio, "numeroExterior", "numero_exterior")
                            ? `No. ${value(domicilio, "numeroExterior", "numero_exterior")}`
                            : null
                        ].filter(Boolean).join(" ") || "Sin dirección capturada"}
                      </div>
                      <div className="text-xs">
                        AGEB {value(domicilio, "cveAgeb", "cve_ageb") || "—"}
                      </div>
                    </div>
                  </Popup>
                </Marker>

                <AjustarMapaAGeB
                  geoJson={residenciaAgebGeoJson}
                  latitud={Number(value(domicilio, "latitud", "latitud"))}
                  longitud={Number(value(domicilio, "longitud", "longitud"))}
                />
              </MapContainer>
            </div>
          </div>
        )}

        <Row
          label="Cartilla"
          value={
            value(p, "tipoCartilla", "tipo_cartilla") ||
            value(p, "versionCartilla", "version_cartilla") ||
            "—"
          }
        />
      </>
    ) : (
      <>
        <div className="rounded-md border border-dashed p-3 text-muted-foreground">
          El paciente todavía no tiene un domicilio georreferenciado registrado.
        </div>

        <Row
          label="Cartilla"
          value={
            value(p, "tipoCartilla", "tipo_cartilla") ||
            value(p, "versionCartilla", "version_cartilla") ||
            "—"
          }
        />
      </>
    )}

  </CardContent>
</Card>
          <Card><CardHeader><CardTitle>Tutor y condiciones</CardTitle></CardHeader><CardContent className="space-y-3 text-sm">
            <Row label="Tutor" value={value(p,"tutorNombre","tutor_nombre")||"—"} /><Row label="CURP tutor" value={value(p,"tutorCurp","tutor_curp")||"—"} mono /><Row label="Teléfono" value={value(p,"tutorTelefono","tutor_telefono")||"—"} /><Row label="Pueblo indígena" value={value(p,"pertencePuebloIndigena","pertence_pueblo_indigena")?"Sí":"No"} /><Row label="Migrante" value={value(p,"esMigrante","es_migrante")?"Sí":"No"} />
          </CardContent></Card>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>Historial de vacunación</CardTitle>
            <CardDescription>{vacunas.length} dosis aplicadas registradas en el expediente</CardDescription>
          </CardHeader>
          <CardContent className="p-0 overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Vacuna</TableHead>
                  <TableHead>Dosis</TableHead>
                  <TableHead>Fecha</TableHead>
                  <TableHead>Lote</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {vacunas.map((v, index) => (
                  <TableRow key={value(v, "id", "id") || index}>
                    <TableCell className="font-medium">{vacunaNombre(v)}</TableCell>
                    <TableCell>{value(v, "numeroDosis", "numero_dosis") ?? "—"}</TableCell>
                    <TableCell className="text-xs">{fechaMX(value(v, "fechaAplicacion", "fecha_aplicacion"))}</TableCell>
                    <TableCell>{value(v, "lote", "lote") || value(v, "numeroLote", "numero_lote") || "—"}</TableCell>
                  </TableRow>
                ))}
                {vacunas.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={4} className="text-center text-muted-foreground py-8">
                      No hay vacunas aplicadas registradas para este paciente.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>

      <Dialog open={bajaOpen} onOpenChange={(open) => { if (!deleting) setBajaOpen(open); }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Dar de baja al paciente</DialogTitle>
          </DialogHeader>
          <form onSubmit={confirmarBaja} className="space-y-4">
            <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm">
              Esta acción retirará a <strong>{nombre || "este paciente"}</strong> de los registros activos.
              Su expediente e historial de vacunación no serán eliminados físicamente.
            </div>
            {bajaError && <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">{bajaError}</div>}
            <Field label="Motivo de la baja *">
              <textarea
                className="flex min-h-24 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                maxLength={500}
                value={motivoBaja}
                onChange={(e) => setMotivoBaja(e.target.value)}
                placeholder="Ej. Registro duplicado, captura incorrecta..."
                disabled={deleting}
                autoFocus
              />
              <div className="text-xs text-muted-foreground text-right">{motivoBaja.length}/500</div>
            </Field>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setBajaOpen(false)} disabled={deleting}>Cancelar</Button>
              <Button type="submit" variant="destructive" disabled={deleting || !motivoBaja.trim()}>
                {deleting && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
                Confirmar baja
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={domicilioOpen} onOpenChange={(open) => { if (!domicilioSaving) setDomicilioOpen(open); }}>
        <DialogContent className="max-w-5xl max-h-[92vh] overflow-y-auto">
          <DialogHeader><DialogTitle>Cambiar domicilio</DialogTitle></DialogHeader>
          {domicilioForm && (
            <form onSubmit={guardarDomicilio} className="space-y-4">
              <div className="rounded-md border p-3 text-sm text-muted-foreground">
                Captura la dirección y selecciona la ubicación en el mapa. El municipio, AGEB y CLUES sugerida se resolverán automáticamente. Al guardar se cerrará el domicilio vigente y se registrará uno nuevo.
              </div>

              {domicilioError && <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">{domicilioError}</div>}

              <div className="grid gap-5 lg:grid-cols-[1fr_1.15fr]">
                <div className="grid gap-4 md:grid-cols-2 content-start">
                  <Field label="Localidad">
                    <Input maxLength={150} value={domicilioForm.localidad} onChange={e=>setDomicilioForm(f=>({...f,localidad:e.target.value}))}/>
                  </Field>
                  <Field label="Clave de localidad">
                    <Input maxLength={10} value={domicilioForm.localidadClave} onChange={e=>setDomicilioForm(f=>({...f,localidadClave:e.target.value}))}/>
                  </Field>
                  <Field label="Colonia">
                    <Input maxLength={150} value={domicilioForm.colonia} onChange={e=>setDomicilioForm(f=>({...f,colonia:e.target.value}))}/>
                  </Field>
                  <Field label="Código postal">
                    <Input maxLength={10} value={domicilioForm.codigoPostal} onChange={e=>setDomicilioForm(f=>({...f,codigoPostal:e.target.value}))}/>
                  </Field>
                  <Field label="Calle">
                    <Input maxLength={200} value={domicilioForm.calle} onChange={e=>setDomicilioForm(f=>({...f,calle:e.target.value}))}/>
                  </Field>
                  <Field label="Número exterior">
                    <Input maxLength={30} value={domicilioForm.numeroExterior} onChange={e=>setDomicilioForm(f=>({...f,numeroExterior:e.target.value}))}/>
                  </Field>
                  <Field label="Número interior">
                    <Input maxLength={30} value={domicilioForm.numeroInterior} onChange={e=>setDomicilioForm(f=>({...f,numeroInterior:e.target.value}))}/>
                  </Field>
                  <Field label="Manzana">
                    <Input maxLength={30} value={domicilioForm.manzana} onChange={e=>setDomicilioForm(f=>({...f,manzana:e.target.value}))}/>
                  </Field>
                  <Field label="Lote">
                    <Input maxLength={30} value={domicilioForm.lote} onChange={e=>setDomicilioForm(f=>({...f,lote:e.target.value}))}/>
                  </Field>

                  <div className="md:col-span-2">
                    <Field label="Dirección / referencia">
                      <Input maxLength={500} value={domicilioForm.direccionTexto} onChange={e=>setDomicilioForm(f=>({...f,direccionTexto:e.target.value}))}/>
                    </Field>
                  </div>
                </div>

                <div className="space-y-3">
                  <div>
                    <Label>Ubicación en mapa *</Label>
                    <p className="text-xs text-muted-foreground mt-1">
                      Busca una dirección o haz clic sobre el mapa y arrastra el marcador para confirmar.
                    </p>
                  </div>

                  <GeocodificadorDireccion disabled={domicilioSaving} onSelect={({ latitud, longitud, direccion }) => {
                    setDomicilioForm(f => ({ ...f,
                      calle: direccion.road || direccion.pedestrian || f.calle || "",
                      colonia: direccion.suburb || direccion.neighbourhood || f.colonia || "",
                      codigoPostal: direccion.postcode || f.codigoPostal || "",
                      localidad: direccion.city || direccion.town || direccion.village || f.localidad || "",
                      numeroExterior: "",
                      numeroInterior: "",
                    }));
                    cambiarUbicacionMapa(latitud, longitud);
                  }}/>
                  <div className="h-[360px] overflow-hidden rounded-md border">
                    <MapContainer
                      key={domicilioOpen ? "domicilio-mapa-abierto" : "domicilio-mapa-cerrado"}
                      center={[
                        Number(domicilioForm.latitud) || 19.432608,
                        Number(domicilioForm.longitud) || -99.133209
                      ]}
                      zoom={
                        domicilioForm.latitud !== "" &&
                        domicilioForm.longitud !== "" &&
                        Number.isFinite(Number(domicilioForm.latitud)) &&
                        Number.isFinite(Number(domicilioForm.longitud))
                          ? 16
                          : 11
                      }
                      scrollWheelZoom
                      className="h-full w-full"
                    >
                      <TileLayer
                        attribution='&copy; OpenStreetMap contributors'
                        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                      />

                      {agebGeoJson && (
                        <GeoJSON
                          key={cvegeoResultado(geoResultado) || "ageb"}
                          data={agebGeoJson}
                          style={() => ({
                            weight: 3,
                            opacity: 0.9,
                            fillOpacity: 0.12,
                          })}
                        />
                      )}

                      {cluesResultado(geoResultado).map((unidad, index) => {
                        const lat = Number(value(unidad, "latitud", "latitud"));
                        const lng = Number(value(unidad, "longitud", "longitud"));
                        if (!Number.isFinite(lat) || !Number.isFinite(lng)) return null;

                        const clues = value(unidad, "clues", "clues") || "—";
                        const nombre =
                          textoCatalogo(value(unidad, "nombre", "nombre")) ||
                          textoCatalogo(value(unidad, "nombreUnidad", "nombre_unidad")) ||
                          "Unidad de salud";
                        const distancia = value(unidad, "distanciaKm", "distancia_km");

                        return (
                          <Marker
                            key={`${clues}-${index}`}
                            position={[lat, lng]}
                            icon={marcadorClues}
                          >
                            <Popup>
                              <div className="space-y-1">
                                <div className="font-semibold">{nombre}</div>
                                <div className="font-mono text-xs">{clues}</div>
                                {distancia != null && (
                                  <div className="text-xs">
                                    {Number(distancia).toFixed(3)} km
                                  </div>
                                )}
                              </div>
                            </Popup>
                          </Marker>
                        );
                      })}

                      <SelectorUbicacion
                        latitud={domicilioForm.latitud === "" ? NaN : Number(domicilioForm.latitud)}
                        longitud={domicilioForm.longitud === "" ? NaN : Number(domicilioForm.longitud)}
                        onChange={cambiarUbicacionMapa}
                      />

                      <AjustarMapaAGeB
                        geoJson={agebGeoJson}
                        latitud={domicilioForm.latitud === "" ? NaN : Number(domicilioForm.latitud)}
                        longitud={domicilioForm.longitud === "" ? NaN : Number(domicilioForm.longitud)}
                      />
                    </MapContainer>
                  </div>

                  <div className="grid grid-cols-2 gap-3 text-xs">
                    <div className="rounded-md border p-2">
                      <span className="text-muted-foreground">Latitud</span>
                      <div className="font-mono mt-1">{domicilioForm.latitud || "—"}</div>
                    </div>
                    <div className="rounded-md border p-2">
                      <span className="text-muted-foreground">Longitud</span>
                      <div className="font-mono mt-1">{domicilioForm.longitud || "—"}</div>
                    </div>
                  </div>

                  {geoResolviendo && (
                    <div className="rounded-md border p-3 text-sm flex items-center gap-2">
                      <Loader2 className="w-4 h-4 animate-spin" />
                      Resolviendo AGEB, municipio y unidades cercanas…
                    </div>
                  )}

                  {geoError && (
                    <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
                      No se pudo completar la clasificación geográfica: {geoError}. Puedes guardar
                      el domicilio con sus coordenadas y dejar el AGEB pendiente, siempre que
                      el servidor acepte el registro y la ubicación esté confirmada.
                    </div>
                  )}

                  {geoResultado && !geoResolviendo && (
                    <div className="rounded-md border p-3 text-sm space-y-2">
                      <div className="font-medium">Ubicación detectada</div>
                      <Row
                        label="Estado"
                        value={
                          textoCatalogo(value(geoResultado, "estadoNombre", "estado_nombre")) ||
                          textoCatalogo(value(geoResultado, "entidadNombre", "entidad_nombre")) ||
                          textoCatalogo(value(geoResultado, "entidad", "entidad")) ||
                          "—"
                        }
                      />
                      <Row
                        label="Municipio / Alcaldía"
                        value={
                          textoCatalogo(value(geoResultado, "municipioNombre", "municipio_nombre")) ||
                          textoCatalogo(value(geoResultado, "municipio", "municipio")) ||
                          "—"
                        }
                      />
                      <Row
                        label="AGEB"
                        value={
                          cveAgebResultado(geoResultado) || "—"
                        }
                        mono
                      />
                      <Row
                        label="CVEGEO"
                        value={
                          cvegeoResultado(geoResultado) || "—"
                        }
                        mono
                      />
                      {agebGeoJsonError && (
                        <div className="rounded-md bg-amber-50 p-2 text-xs text-amber-800">
                          {agebGeoJsonError}
                        </div>
                      )}

                      {cluesResultado(geoResultado).length > 0 && (
                        <div className="pt-2 border-t">
                          <div className="font-medium mb-2">Unidades cercanas</div>
                          <div className="space-y-2">
                            {cluesResultado(geoResultado).map((unidad, index) => {
                              const clues = value(unidad, "clues", "clues") || "—";
                              const nombre =
                                textoCatalogo(value(unidad, "nombre", "nombre")) ||
                                textoCatalogo(value(unidad, "nombreUnidad", "nombre_unidad")) ||
                                "Unidad de salud";
                              const distancia = value(unidad, "distanciaKm", "distancia_km");
                              const institucion = value(unidad, "institucion", "institucion");

                              return (
                                <div
                                  key={`${clues}-${index}`}
                                  className="rounded-md bg-muted/40 p-2"
                                >
                                  <div className="flex items-start gap-2">
                                    <Badge variant={index === 0 ? "default" : "outline"}>
                                      {index + 1}
                                    </Badge>
                                    <div className="min-w-0">
                                      <div className="font-medium">{nombre}</div>
                                      <div className="font-mono text-xs">{clues}</div>
                                      <div className="text-xs text-muted-foreground">
                                        {institucion || "Institución no especificada"}
                                        {distancia != null
                                          ? ` · ${Number(distancia).toFixed(3)} km`
                                          : ""}
                                      </div>
                                    </div>
                                  </div>
                                </div>
                              );
                            })}
                          </div>
                          <p className="text-xs text-muted-foreground mt-2">
                            La primera unidad es una sugerencia por cercanía geográfica; no representa adscripción administrativa.
                          </p>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </div>

              <DialogFooter>
                <Button type="button" variant="outline" onClick={()=>setDomicilioOpen(false)} disabled={domicilioSaving}>
                  Cancelar
                </Button>
                <Button type="submit" disabled={domicilioSaving || geoResolviendo || !geoResultado}>
                  {domicilioSaving && <Loader2 className="w-4 h-4 mr-2 animate-spin"/>}
                  Guardar nuevo domicilio
                </Button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={historialOpen} onOpenChange={setHistorialOpen}>
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
          <DialogHeader><DialogTitle>Historial de domicilios</DialogTitle></DialogHeader>
          <div className="space-y-3">
            {historialDomicilios.map((d, i) => (
              <Card key={value(d,"id","id") || i}>
                <CardContent className="p-4 space-y-2 text-sm">
                  <div className="flex justify-between gap-3"><strong>{value(d,"direccionTexto","direccion_texto") || "Domicilio"}</strong>{value(d,"vigente","vigente") ? <Badge>Vigente</Badge> : <Badge variant="outline">Histórico</Badge>}</div>
                  <Row label="Estado" value={value(d,"estadoNombre","estado_nombre") || "—"}/>
                  <Row label="Municipio / Alcaldía" value={value(d,"municipioNombre","municipio_nombre") || "—"}/>
                  <Row label="Localidad" value={value(d,"localidad","localidad") || "—"}/>
                  <Row label="AGEB" value={value(d,"cveAgeb","cve_ageb") || "—"} mono/>
                  <Row label="CVEGEO" value={value(d,"cvegeo","cvegeo") || "—"} mono/>
                  <Row label="CLUES sugerida" value={value(d,"cluesSugerida","clues_sugerida") || "—"} mono/>
                  <Row label="Inicio" value={fechaMX(value(d,"fechaInicio","fecha_inicio"))}/>
                  <Row label="Fin" value={fechaMX(value(d,"fechaFin","fecha_fin"))}/>
                </CardContent>
              </Card>
            ))}
            {historialDomicilios.length === 0 && <p className="text-sm text-muted-foreground">No hay domicilios registrados.</p>}
          </div>
        </DialogContent>
      </Dialog>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto"><DialogHeader><DialogTitle>Editar datos del paciente</DialogTitle></DialogHeader>
          {form && <form onSubmit={guardarEdicion} className="space-y-4">
            {formError && <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">{formError}</div>}
            <div className="grid gap-4 md:grid-cols-2">
              <Field label="CURP"><Input maxLength={18} value={form.curp} onChange={e=>setForm(f=>({...f,curp:e.target.value.toUpperCase()}))}/></Field>
              <Field label="Nombre"><Input value={form.nombre} onChange={e=>setForm(f=>({...f,nombre:e.target.value}))}/></Field>
              <Field label="Primer apellido"><Input value={form.primerApellido} onChange={e=>setForm(f=>({...f,primerApellido:e.target.value}))}/></Field>
              <Field label="Segundo apellido"><Input value={form.segundoApellido} onChange={e=>setForm(f=>({...f,segundoApellido:e.target.value}))}/></Field>
              <Field label="Fecha de nacimiento"><Input type="date" value={form.fechaNacimiento} onChange={e=>setForm(f=>({...f,fechaNacimiento:e.target.value}))}/></Field>
              <Field label="Sexo"><Select value={form.sexo} onValueChange={v=>setForm(f=>({...f,sexo:v}))}><SelectTrigger><SelectValue/></SelectTrigger><SelectContent><SelectItem value="F">Femenino</SelectItem><SelectItem value="M">Masculino</SelectItem></SelectContent></Select></Field>
              <Field label="Entidad de nacimiento"><Select value={form.entidadNacimientoClave} onValueChange={v=>setForm(f=>({...f,entidadNacimientoClave:v}))}><SelectTrigger><SelectValue/></SelectTrigger><SelectContent>{estados.map(x=><SelectItem key={value(x,"claveInegi","clave_inegi")} value={String(value(x,"claveInegi","clave_inegi"))}>{value(x,"nombre","nombre")}</SelectItem>)}</SelectContent></Select></Field>
           
              <Field label="Tipo de cartilla"><Select value={form.tipoCartillaId} onValueChange={v=>setForm(f=>({...f,tipoCartillaId:v}))}><SelectTrigger><SelectValue/></SelectTrigger><SelectContent>{tiposCartilla.map(x=><SelectItem key={value(x,"id","id")} value={String(value(x,"id","id"))}>{value(x,"nombre","nombre")}</SelectItem>)}</SelectContent></Select></Field>
          
              <Field label="Nombre del tutor"><Input value={form.tutorNombre} onChange={e=>setForm(f=>({...f,tutorNombre:e.target.value}))}/></Field>
              <Field label="CURP del tutor"><Input maxLength={18} value={form.tutorCurp} onChange={e=>setForm(f=>({...f,tutorCurp:e.target.value.toUpperCase()}))}/></Field>
              <Field label="Teléfono"><Input maxLength={20} value={form.tutorTelefono} onChange={e=>setForm(f=>({...f,tutorTelefono:e.target.value}))}/></Field>
              <div className="space-y-3"><label className="flex items-center gap-2 text-sm"><Checkbox checked={form.pertencePuebloIndigena} onCheckedChange={v=>setForm(f=>({...f,pertencePuebloIndigena:v===true}))}/>Pertenece a pueblo indígena</label><label className="flex items-center gap-2 text-sm"><Checkbox checked={form.esMigrante} onCheckedChange={v=>setForm(f=>({...f,esMigrante:v===true}))}/>Es migrante</label></div>
            </div>
            <DialogFooter><Button type="button" variant="outline" onClick={()=>setEditOpen(false)} disabled={saving}>Cancelar</Button><Button type="submit" disabled={saving}>{saving&&<Loader2 className="w-4 h-4 mr-2 animate-spin"/>}Guardar cambios</Button></DialogFooter>
          </form>}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Row({ label, value: rowValue, mono = false }) {
  return (
    <div className="flex justify-between gap-4">
      <span className="text-muted-foreground">{label}</span>
      <span className={`font-medium text-right ${mono ? "font-mono" : ""}`}>{rowValue}</span>
    </div>
  );
}

function Field({label,children}) { return <div className="space-y-1.5"><Label>{label}</Label>{children}</div>; }
