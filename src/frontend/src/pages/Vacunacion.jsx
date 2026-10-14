import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Loader2,
  Search,
  ShieldAlert,
  Syringe,
  UserRound,
  Ban,
  RotateCcw,
  History,
} from "lucide-react";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

import { useToast } from "@/components/ui/use-toast";
import apiClient from "@/api/apiClient";

const hoyIso = () => new Date().toISOString().slice(0, 10);

const v = (obj, camel, snake = camel) => {
  if (!obj) return undefined;
  const pascal = camel.charAt(0).toUpperCase() + camel.slice(1);
  return obj[camel] ?? obj[pascal] ?? obj[snake];
};

const arr = (obj, camel, snake = camel) => {
  const x = v(obj, camel, snake);
  return Array.isArray(x) ? x : [];
};

function nombreCompleto(p) {
  return [v(p, "nombre"), v(p, "primerApellido", "primer_apellido"), v(p, "segundoApellido", "segundo_apellido")]
    .filter(Boolean)
    .join(" ");
}

function edadAnios(fecha) {
  if (!fecha) return null;
  const n = new Date(fecha);
  if (Number.isNaN(n.getTime())) return null;
  const h = new Date();
  let e = h.getFullYear() - n.getFullYear();
  const m = h.getMonth() - n.getMonth();
  if (m < 0 || (m === 0 && h.getDate() < n.getDate())) e--;
  return e;
}

function fechaCorta(fecha) {
  if (!fecha) return "—";
  const d = new Date(fecha);
  if (Number.isNaN(d.getTime())) return String(fecha).slice(0, 10);
  return d.toLocaleDateString("es-MX");
}

function fechaHora(fecha) {
  if (!fecha) return "—";
  const d = new Date(fecha);
  if (Number.isNaN(d.getTime())) return String(fecha);
  return d.toLocaleString("es-MX");
}

function jsonSeguro(valor) {
  if (!valor) return {};
  if (typeof valor === "object") return valor;
  try {
    return JSON.parse(valor);
  } catch {
    return {};
  }
}

function dosisTexto(item) {
  const n = v(item, "numeroDosis", "numero_dosis");
  const tipo = v(item, "tipoDosis", "tipo_dosis");
  return tipo ? `${tipo} · dosis ${n}` : `Dosis ${n}`;
}

function ItemEsquema({ item, tipo, onElegir }) {
  const aplicable = tipo === "aplicable";
  const programada = tipo === "programada";

  return (
    <div className="rounded-lg border p-3 space-y-1.5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="font-medium">{v(item, "vacuna") || "Vacuna"}</div>
          <div className="text-xs text-muted-foreground">
            {v(item, "claveAnx", "clave_anx") || "—"} · {dosisTexto(item)}
          </div>
        </div>
        {aplicable && (
          <Button size="sm" onClick={() => onElegir(item)}>
            Seleccionar
          </Button>
        )}
      </div>

      {v(item, "motivo") && (
        <p className="text-xs text-muted-foreground">{v(item, "motivo")}</p>
      )}

      {programada && v(item, "fechaMinimaAplicacion", "fecha_minima_aplicacion") && (
        <p className="text-xs">
          Fecha mínima: <strong>{fechaCorta(v(item, "fechaMinimaAplicacion", "fecha_minima_aplicacion"))}</strong>
        </p>
      )}

      {v(item, "grupoPoblacional", "grupo_poblacional") && (
        <Badge variant="outline" className="text-[10px]">
          {v(item, "grupoPoblacional", "grupo_poblacional")}
        </Badge>
      )}
    </div>
  );
}

export default function Vacunacion() {
  const { toast } = useToast();
  const [searchParams, setSearchParams] = useSearchParams();

  const citaPacienteId = searchParams.get("pacienteId");
  const citaVacunaId = searchParams.get("vacunaId");
  const citaDosis = searchParams.get("dosis");

  const [q, setQ] = useState("");
  const [pacientes, setPacientes] = useState([]);
  const [totalPacientes, setTotalPacientes] = useState(0);
  const [cargandoPacientes, setCargandoPacientes] = useState(false);
  const [errorPacientes, setErrorPacientes] = useState("");

  const [pacienteSel, setPacienteSel] = useState(null);
  const [esquema, setEsquema] = useState(null);
  const [cargandoEsquema, setCargandoEsquema] = useState(false);
  const [errorEsquema, setErrorEsquema] = useState("");

  const [dosisSel, setDosisSel] = useState(null);
  const [guardando, setGuardando] = useState(false);
  const [form, setForm] = useState({
    fechaAplicacion: hoyIso(),
    lote: "",
    laboratorio: "",
    observaciones: "",
    grupoPoblacional: "",
  });

  const [anulacionOpen, setAnulacionOpen] = useState(false);
  const [aplicacionAnular, setAplicacionAnular] = useState(null);
  const [motivoAnulacion, setMotivoAnulacion] = useState("");
  const [anulando, setAnulando] = useState(false);

  const [auditoriaOpen, setAuditoriaOpen] = useState(false);
  const [auditoria, setAuditoria] = useState([]);
  const [cargandoAuditoria, setCargandoAuditoria] = useState(false);
  const [errorAuditoria, setErrorAuditoria] = useState("");
  const [cargandoCita, setCargandoCita] = useState(false);
  const [citaPrecargada, setCitaPrecargada] = useState(false);

  const aplicables = useMemo(() => arr(esquema, "aplicables"), [esquema]);
  const programadas = useMemo(() => arr(esquema, "programadas"), [esquema]);
  const verificables = useMemo(
    () => arr(esquema, "requierenVerificacion", "requieren_verificacion"),
    [esquema]
  );
  const aplicadas = useMemo(() => arr(esquema, "aplicadas"), [esquema]);

  const buscarPacientes = useCallback(async (texto) => {
    setCargandoPacientes(true);
    setErrorPacientes("");

    try {
      const termino = texto.trim();
      const query = new URLSearchParams({
        page: "1",
        pageSize: "25",
      });

      if (termino) query.set("nombre", termino);

      const data = await apiClient.get(`/pacientes?${query.toString()}`);
      setPacientes(arr(data, "items"));
      setTotalPacientes(Number(v(data, "total") || 0));
    } catch (error) {
      setPacientes([]);
      setTotalPacientes(0);
      setErrorPacientes(error.message || "No fue posible consultar pacientes.");
    } finally {
      setCargandoPacientes(false);
    }
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => buscarPacientes(q), 300);
    return () => clearTimeout(timer);
  }, [q, buscarPacientes]);

  const cargarEsquema = useCallback(async (paciente) => {
    if (!paciente?.id) return;

    setCargandoEsquema(true);
    setErrorEsquema("");
    setDosisSel(null);

    try {
      const data = await apiClient.get(
        `/pacientes/${encodeURIComponent(paciente.id)}/esquema-vacunacion`
      );
      setEsquema(data);
      return data;
    } catch (error) {
      setEsquema(null);
      setErrorEsquema(error.message || "No fue posible calcular el esquema.");
      return null;
    } finally {
      setCargandoEsquema(false);
    }
  }, []);

  const seleccionarPaciente = async (paciente) => {
    setPacienteSel(paciente);
    setForm({
      fechaAplicacion: hoyIso(),
      lote: "",
      laboratorio: "",
      observaciones: "",
      grupoPoblacional: "",
    });
    await cargarEsquema(paciente);
  };

  // ==========================================================
  // Abrir vacunación desde una cita
  // URL esperada:
  // /vacunacion?pacienteId=...&vacunaId=...&dosis=...
  // ==========================================================
  useEffect(() => {
    if (
      citaPrecargada ||
      !citaPacienteId ||
      !citaVacunaId ||
      !citaDosis
    ) {
      return;
    }

    let cancelado = false;

    const cargarDesdeCita = async () => {
      setCargandoCita(true);

      try {
        const paciente = await apiClient.get(
          `/pacientes/${encodeURIComponent(citaPacienteId)}`
        );

        if (cancelado) return;

        setPacienteSel(paciente);
        setForm({
          fechaAplicacion: hoyIso(),
          lote: "",
          laboratorio: "",
          observaciones: "",
          grupoPoblacional: "",
        });

        const nuevoEsquema = await cargarEsquema(paciente);

        if (cancelado || !nuevoEsquema) return;

        const vacunaId = Number(citaVacunaId);
        const numeroDosis = Number(citaDosis);

        const dosisAplicable = arr(nuevoEsquema, "aplicables").find(
          (item) =>
            Number(v(item, "vacunaId", "vacuna_id")) === vacunaId &&
            Number(v(item, "numeroDosis", "numero_dosis")) === numeroDosis
        );

        if (dosisAplicable) {
          setDosisSel(dosisAplicable);
          setForm((f) => ({
            ...f,
            grupoPoblacional:
              v(
                dosisAplicable,
                "grupoPoblacional",
                "grupo_poblacional"
              ) || "",
          }));

          toast({
            title: "Cita cargada",
            description:
              `${v(dosisAplicable, "vacuna")} · dosis ${numeroDosis}. ` +
              "Completa lote y datos de aplicación.",
          });
        } else {
          toast({
            title: "La dosis de la cita ya no es aplicable",
            description:
              "El motor recalculó el esquema y no autoriza actualmente esa vacuna/dosis. No se preseleccionó ninguna aplicación.",
            variant: "destructive",
          });
        }

        setCitaPrecargada(true);

        // Limpiamos los parámetros para que un recálculo posterior del
        // componente no vuelva a forzar la selección de la cita.
        setSearchParams({}, { replace: true });
      } catch (error) {
        if (cancelado) return;

        setCitaPrecargada(true);

        toast({
          title: "No fue posible abrir la cita",
          description:
            error.message ||
            "No se pudo cargar el paciente o recalcular su esquema.",
          variant: "destructive",
        });
      } finally {
        if (!cancelado) {
          setCargandoCita(false);
        }
      }
    };

    cargarDesdeCita();

    return () => {
      cancelado = true;
    };
  }, [
    citaPacienteId,
    citaVacunaId,
    citaDosis,
    citaPrecargada,
    cargarEsquema,
    setSearchParams,
    toast,
  ]);

  const elegirDosis = (item) => {
    setDosisSel(item);
    setForm((f) => ({
      ...f,
      grupoPoblacional: v(item, "grupoPoblacional", "grupo_poblacional") || "",
    }));
  };

  const registrar = async () => {
    if (!pacienteSel || !dosisSel) {
      toast({
        title: "Selecciona una dosis",
        description: "Sólo pueden registrarse dosis marcadas como aplicables por el motor.",
        variant: "destructive",
      });
      return;
    }

    if (!form.fechaAplicacion || !form.lote.trim()) {
      toast({
        title: "Faltan datos",
        description: "La fecha de aplicación y el lote son obligatorios.",
        variant: "destructive",
      });
      return;
    }

    setGuardando(true);
    try {
      await apiClient.post(`/pacientes/${pacienteSel.id}/vacunaciones`, {
        vacunaId: Number(v(dosisSel, "vacunaId", "vacuna_id")),
        numeroDosis: Number(v(dosisSel, "numeroDosis", "numero_dosis")),
        fechaAplicacion: `${form.fechaAplicacion}T00:00:00`,
        lote: form.lote.trim(),
        laboratorio: form.laboratorio.trim() || null,
        observaciones: form.observaciones.trim() || null,
        grupoPoblacional: form.grupoPoblacional.trim() || null,
      });

      toast({
        title: "Dosis registrada",
        description: `${v(dosisSel, "vacuna")} · dosis ${v(dosisSel, "numeroDosis", "numero_dosis")}.`,
      });

      setDosisSel(null);
      setForm({
        fechaAplicacion: hoyIso(),
        lote: "",
        laboratorio: "",
        observaciones: "",
        grupoPoblacional: "",
      });

      await cargarEsquema(pacienteSel);
    } catch (error) {
      toast({
        title: "No fue posible registrar la dosis",
        description: error.message || "La API rechazó la aplicación.",
        variant: "destructive",
      });
    } finally {
      setGuardando(false);
    }
  };

  const abrirAnulacion = (aplicacion) => {
    setAplicacionAnular(aplicacion);
    setMotivoAnulacion("");
    setAnulacionOpen(true);
  };

  const cerrarAnulacion = () => {
    if (anulando) return;
    setAnulacionOpen(false);
    setAplicacionAnular(null);
    setMotivoAnulacion("");
  };

  const anularAplicacion = async (prepararCorreccion = false) => {
    if (!pacienteSel || !aplicacionAnular) return;

    const motivo = motivoAnulacion.trim();
    if (motivo.length < 5) {
      toast({
        title: "Motivo requerido",
        description: "Escribe un motivo de al menos 5 caracteres para conservar la trazabilidad.",
        variant: "destructive",
      });
      return;
    }

    const aplicacionId = v(aplicacionAnular, "aplicacionId", "aplicacion_id");
    if (!aplicacionId) {
      toast({
        title: "Aplicación inválida",
        description: "No se encontró el identificador de la aplicación.",
        variant: "destructive",
      });
      return;
    }

    const vacunaId = Number(v(aplicacionAnular, "vacunaId", "vacuna_id"));
    const numeroDosis = Number(v(aplicacionAnular, "numeroDosis", "numero_dosis"));

    setAnulando(true);
    try {
      await apiClient.post(
        `/pacientes/${pacienteSel.id}/vacunaciones/${aplicacionId}/anular`,
        { motivo }
      );

      setAnulacionOpen(false);
      setAplicacionAnular(null);
      setMotivoAnulacion("");

      const nuevoEsquema = await cargarEsquema(pacienteSel);

      if (prepararCorreccion && nuevoEsquema) {
        const nuevaAplicable = arr(nuevoEsquema, "aplicables").find(
          (x) =>
            Number(v(x, "vacunaId", "vacuna_id")) === vacunaId &&
            Number(v(x, "numeroDosis", "numero_dosis")) === numeroDosis
        );

        if (nuevaAplicable) {
          elegirDosis(nuevaAplicable);
          toast({
            title: "Aplicación anulada",
            description:
              "La dosis volvió a quedar aplicable. Completa los datos correctos y registra la nueva aplicación.",
          });
        } else {
          toast({
            title: "Aplicación anulada",
            description:
              "El motor recalculó el esquema, pero esa misma dosis no quedó aplicable automáticamente. Revisa la recomendación actual.",
          });
        }
      } else {
        toast({
          title: "Aplicación anulada",
          description:
            "La dosis dejó de contar en el esquema y la auditoría quedó registrada.",
        });
      }
    } catch (error) {
      toast({
        title: "No fue posible anular la aplicación",
        description: error.message || "La API rechazó la anulación.",
        variant: "destructive",
      });
    } finally {
      setAnulando(false);
    }
  };

  const abrirAuditoria = async () => {
    if (!pacienteSel?.id) return;

    setAuditoriaOpen(true);
    setCargandoAuditoria(true);
    setErrorAuditoria("");
    setAuditoria([]);

    try {
      const data = await apiClient.get(
        `/pacientes/${encodeURIComponent(pacienteSel.id)}/vacunaciones/auditoria`
      );
      setAuditoria(arr(data, "eventos"));
    } catch (error) {
      setErrorAuditoria(error.message || "No fue posible consultar la auditoría.");
    } finally {
      setCargandoAuditoria(false);
    }
  };

  const cancelarPaciente = () => {
    setPacienteSel(null);
    setEsquema(null);
    setDosisSel(null);
    setErrorEsquema("");
  };

  const recomendacion = v(esquema, "recomendacion");
  const edadMotor = v(esquema, "edad");

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-heading font-semibold">Vacunación</h1>
        <p className="text-muted-foreground text-sm mt-1">
          Registra dosis autorizadas por el motor de esquema y consulta el historial del paciente.
        </p>
      </div>

      {cargandoCita && (
        <div className="rounded-lg border bg-muted/30 p-3 flex items-center gap-2 text-sm">
          <Loader2 className="h-4 w-4 animate-spin" />
          Cargando paciente y validando la dosis de la cita…
        </div>
      )}

      <div className="grid gap-4 xl:grid-cols-[420px_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Seleccionar paciente</CardTitle>
            <CardDescription>
              Pacientes activos registrados en SNCEV
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="relative">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Nombre o apellidos..."
                className="pl-9"
              />
            </div>

            {cargandoPacientes && (
              <div className="flex items-center gap-2 py-5 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Consultando pacientes...
              </div>
            )}

            {errorPacientes && (
              <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
                {errorPacientes}
              </div>
            )}

            {!cargandoPacientes && !errorPacientes && (
              <>
                <div className="text-xs text-muted-foreground">
                  {totalPacientes} paciente{totalPacientes === 1 ? "" : "s"} encontrado{totalPacientes === 1 ? "" : "s"}
                </div>

                <div className="h-[420px] overflow-y-auto overscroll-contain pr-1 divide-y">
                  {pacientes.map((p) => {
                    const activo = pacienteSel?.id === p.id;
                    const edad = edadAnios(v(p, "fechaNacimiento", "fecha_nacimiento"));

                    return (
                      <button
                        type="button"
                        key={p.id}
                        onClick={() => seleccionarPaciente(p)}
                        className={`w-full text-left py-3 px-2 rounded-md transition ${
                          activo ? "bg-muted" : "hover:bg-muted/50"
                        }`}
                      >
                        <div className="font-medium text-sm">{nombreCompleto(p)}</div>
                        <div className="flex items-center justify-between gap-2 mt-1">
                          <div className="text-xs text-muted-foreground">
                            {edad == null ? "Edad no disponible" : `${edad} años`}
                            {v(p, "curp") ? ` · ${v(p, "curp")}` : ""}
                          </div>
                          {activo && <Badge>Seleccionado</Badge>}
                        </div>
                      </button>
                    );
                  })}

                  {pacientes.length === 0 && (
                    <div className="py-8 text-center text-sm text-muted-foreground">
                      No se encontraron pacientes.
                    </div>
                  )}
                </div>
              </>
            )}
          </CardContent>
        </Card>

        <div className="space-y-4">
      <Card>
            <CardHeader>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <CardTitle>Aplicaciones vigentes del paciente</CardTitle>
                  <CardDescription>
                    {pacienteSel
                      ? "Historial vigente. Usa Anular / corregir en la última columna para rectificar una dosis sin borrar el antecedente."
                      : "Selecciona un paciente para consultar sus aplicaciones."}
                  </CardDescription>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={abrirAuditoria}
                  disabled={!pacienteSel || cargandoEsquema}
                >
                  <History className="h-4 w-4 mr-2" />
                  Ver auditoría
                </Button>
              </div>
            </CardHeader>

        <CardContent className="p-0">
          {!pacienteSel ? (
            <div className="py-10 text-center text-sm text-muted-foreground">
              Sin paciente seleccionado.
            </div>
          ) : cargandoEsquema ? (
            <div className="py-10 flex justify-center">
              <Loader2 className="h-5 w-5 animate-spin" />
            </div>
          ) : aplicadas.length === 0 ? (
            <div className="py-10 text-center text-sm text-muted-foreground">
              El paciente no tiene aplicaciones vigentes registradas.
            </div>
          ) : (
            <div className="h-[210px] overflow-auto overscroll-contain border-t">
              <Table className="min-w-[980px]">
              <TableHeader>
                <TableRow>
                  <TableHead>Vacuna</TableHead>
                  <TableHead>Dosis</TableHead>
                  <TableHead>Fecha</TableHead>
                  <TableHead>Lote</TableHead>
                  <TableHead>Laboratorio</TableHead>
                  <TableHead>CLUES</TableHead>
                  <TableHead>Estado</TableHead>
                  <TableHead className="text-right">Acciones</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {[...aplicadas]
                  .sort(
                    (a, b) =>
                      new Date(v(b, "fechaAplicacion", "fecha_aplicacion")) -
                      new Date(v(a, "fechaAplicacion", "fecha_aplicacion"))
                  )
                  .map((a) => (
                    <TableRow key={v(a, "aplicacionId", "aplicacion_id")}>
                      <TableCell>
                        <div className="font-medium">{v(a, "vacuna")}</div>
                        <div className="text-xs text-muted-foreground">
                          {v(a, "claveAnx", "clave_anx")}
                        </div>
                      </TableCell>
                      <TableCell>{v(a, "numeroDosis", "numero_dosis")}</TableCell>
                      <TableCell>{fechaCorta(v(a, "fechaAplicacion", "fecha_aplicacion"))}</TableCell>
                      <TableCell className="font-mono text-xs">{v(a, "lote") || "—"}</TableCell>
                      <TableCell>{v(a, "laboratorio") || "—"}</TableCell>
                      <TableCell className="font-mono text-xs">
                        {v(a, "unidadMedicaClues", "unidad_medica_clues") || "—"}
                      </TableCell>
                      <TableCell>
                        <Badge className="bg-emerald-100 text-emerald-700">
                          <CheckCircle2 className="w-3 h-3 mr-1" />
                          Vigente
                        </Badge>
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => abrirAnulacion(a)}
                        >
                          <Ban className="h-3.5 w-3.5 mr-1.5" />
                          Anular / corregir
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
              </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
          <Card>
            <CardHeader>
              <CardTitle>Registrar aplicación</CardTitle>
              <CardDescription>
                {pacienteSel
                  ? "El servidor determina qué dosis pueden registrarse."
                  : "Selecciona un paciente para continuar."}
              </CardDescription>
            </CardHeader>

            <CardContent>
              {!pacienteSel ? (
                <div className="text-center py-14 text-muted-foreground">
                  <Syringe className="w-9 h-9 mx-auto mb-3 opacity-40" />
                  <p className="text-sm">
                    Busca y selecciona un paciente para registrar una dosis.
                  </p>
                </div>
              ) : cargandoEsquema ? (
                <div className="flex items-center justify-center gap-2 py-14 text-muted-foreground">
                  <Loader2 className="h-5 w-5 animate-spin" />
                  Calculando esquema...
                </div>
              ) : errorEsquema ? (
                <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-destructive">
                  {errorEsquema}
                </div>
              ) : (
                <div className="space-y-5">
                  <div className="flex flex-wrap items-start justify-between gap-3 rounded-lg border p-4">
                    <div>
                      <div className="flex items-center gap-2">
                        <UserRound className="h-4 w-4" />
                        <span className="font-semibold">
                          {v(esquema, "nombrePaciente", "nombre_paciente") || nombreCompleto(pacienteSel)}
                        </span>
                      </div>
                      <div className="text-xs text-muted-foreground mt-1">
                        {v(edadMotor, "texto") ||
                          `${edadAnios(v(pacienteSel, "fechaNacimiento", "fecha_nacimiento")) ?? "—"} años`}
                        {" · "}
                        Sexo {v(esquema, "sexo") || v(pacienteSel, "sexo") || "—"}
                      </div>
                    </div>
                    <Button variant="outline" size="sm" asChild>
                      <Link to={`/pacientes/${pacienteSel.id}`}>Ver expediente</Link>
                    </Button>
                  </div>

                  {recomendacion && (
                    <div className="rounded-lg border bg-muted/30 p-4">
                      <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                        Recomendación del motor
                      </div>
                      <div className="font-medium mt-1">
                        {v(recomendacion, "vacuna")} · dosis {v(recomendacion, "numeroDosis", "numero_dosis")}
                      </div>
                      <p className="text-xs text-muted-foreground mt-1">
                        {v(recomendacion, "motivo")}
                      </p>
                    </div>
                  )}

                  <div className="grid gap-4 lg:grid-cols-3">
                    <div className="min-w-0">
                      <div className="mb-2 flex items-center gap-2 font-medium text-sm">
                        <CheckCircle2 className="h-4 w-4" />
                        Aplicables ahora
                        <Badge variant="secondary">{aplicables.length}</Badge>
                      </div>

                      <div className="h-[420px] overflow-y-auto overscroll-contain pr-2 space-y-2">
                        {aplicables.length ? (
                          aplicables.map((item) => (
                            <ItemEsquema
                              key={`a-${v(item, "vacunaId", "vacuna_id")}-${v(item, "numeroDosis", "numero_dosis")}`}
                              item={item}
                              tipo="aplicable"
                              onElegir={elegirDosis}
                            />
                          ))
                        ) : (
                          <p className="text-xs text-muted-foreground rounded-md border p-3">
                            No hay dosis automáticamente aplicables hoy.
                          </p>
                        )}
                      </div>
                    </div>

                    <div className="min-w-0">
                      <div className="mb-2 flex items-center gap-2 font-medium text-sm">
                        <Clock className="h-4 w-4" />
                        Programadas
                        <Badge variant="secondary">{programadas.length}</Badge>
                      </div>

                      <div className="h-[420px] overflow-y-auto overscroll-contain pr-2 space-y-2">
                        {programadas.length ? (
                          programadas.map((item) => (
                            <ItemEsquema
                              key={`p-${v(item, "vacunaId", "vacuna_id")}-${v(item, "numeroDosis", "numero_dosis")}`}
                              item={item}
                              tipo="programada"
                              onElegir={elegirDosis}
                            />
                          ))
                        ) : (
                          <p className="text-xs text-muted-foreground rounded-md border p-3">
                            Sin dosis programadas.
                          </p>
                        )}
                      </div>
                    </div>

                    <div className="min-w-0">
                      <div className="mb-2 flex items-center gap-2 font-medium text-sm">
                        <ShieldAlert className="h-4 w-4" />
                        Requieren verificación
                        <Badge variant="secondary">{verificables.length}</Badge>
                      </div>

                      <div className="h-[420px] overflow-y-auto overscroll-contain pr-2 space-y-2">
                        {verificables.length ? (
                          verificables.map((item) => (
                            <ItemEsquema
                              key={`v-${v(item, "vacunaId", "vacuna_id")}-${v(item, "numeroDosis", "numero_dosis")}`}
                              item={item}
                              tipo="verificacion"
                              onElegir={elegirDosis}
                            />
                          ))
                        ) : (
                          <p className="text-xs text-muted-foreground rounded-md border p-3">
                            Sin reglas pendientes de verificación.
                          </p>
                        )}
                      </div>
                    </div>
                  </div>

                  <div className="border-t pt-5">
                    <div className="font-medium mb-3">Datos de la aplicación</div>

                    {!dosisSel ? (
                      <div className="rounded-lg border border-dashed p-5 text-center text-sm text-muted-foreground">
                        Selecciona una dosis de <strong>Aplicables ahora</strong>.
                      </div>
                    ) : (
                      <div className="space-y-4">
                        <div className="rounded-lg border p-3">
                          <div className="font-medium">{v(dosisSel, "vacuna")}</div>
                          <div className="text-xs text-muted-foreground">
                            {v(dosisSel, "claveAnx", "clave_anx")} · {dosisTexto(dosisSel)}
                          </div>
                        </div>

                        <div className="grid gap-4 md:grid-cols-2">
                          <div className="space-y-1.5">
                            <Label>Fecha de aplicación *</Label>
                            <Input
                              type="date"
                              max={hoyIso()}
                              value={form.fechaAplicacion}
                              onChange={(e) =>
                                setForm({ ...form, fechaAplicacion: e.target.value })
                              }
                            />
                          </div>

                          <div className="space-y-1.5">
                            <Label>Lote *</Label>
                            <Input
                              value={form.lote}
                              maxLength={50}
                              onChange={(e) => setForm({ ...form, lote: e.target.value })}
                              placeholder="Número de lote"
                            />
                          </div>

                          <div className="space-y-1.5">
                            <Label>Laboratorio</Label>
                            <Input
                              value={form.laboratorio}
                              maxLength={100}
                              onChange={(e) =>
                                setForm({ ...form, laboratorio: e.target.value })
                              }
                              placeholder="Fabricante / laboratorio"
                            />
                          </div>

                          <div className="space-y-1.5">
                            <Label>Grupo poblacional</Label>
                            <Input
                              value={form.grupoPoblacional}
                              maxLength={150}
                              onChange={(e) =>
                                setForm({ ...form, grupoPoblacional: e.target.value })
                              }
                              placeholder="Opcional"
                            />
                          </div>

                          <div className="space-y-1.5 md:col-span-2">
                            <Label>Observaciones</Label>
                            <Input
                              value={form.observaciones}
                              maxLength={255}
                              onChange={(e) =>
                                setForm({ ...form, observaciones: e.target.value })
                              }
                              placeholder="Observaciones opcionales"
                            />
                          </div>
                        </div>

                        <div className="rounded-md bg-muted/40 p-3 text-xs text-muted-foreground">
                          La CLUES y la identidad del usuario aplicador se obtienen de la sesión autenticada; no se capturan manualmente.
                        </div>

                        <div className="flex justify-end gap-2">
                          <Button
                            variant="outline"
                            onClick={() => setDosisSel(null)}
                            disabled={guardando}
                          >
                            Cancelar dosis
                          </Button>
                          <Button onClick={registrar} disabled={guardando}>
                            {guardando ? (
                              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                            ) : (
                              <Syringe className="w-4 h-4 mr-2" />
                            )}
                            Registrar dosis
                          </Button>
                        </div>
                      </div>
                    )}
                  </div>

                  <div className="flex justify-end">
                    <Button variant="ghost" onClick={cancelarPaciente}>
                      Cerrar paciente
                    </Button>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>



      <Dialog open={auditoriaOpen} onOpenChange={setAuditoriaOpen}>
        <DialogContent className="sm:max-w-[760px] max-h-[85vh] overflow-hidden flex flex-col">
          <DialogHeader>
            <DialogTitle>Auditoría de vacunación</DialogTitle>
            <DialogDescription>
              {pacienteSel
                ? `${nombreCompleto(pacienteSel)} · trazabilidad de aplicaciones y anulaciones`
                : "Trazabilidad de vacunación"}
            </DialogDescription>
          </DialogHeader>

          <div className="flex-1 overflow-y-auto overscroll-contain pr-2">
            {cargandoAuditoria ? (
              <div className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
                <Loader2 className="h-5 w-5 animate-spin" />
                Consultando auditoría...
              </div>
            ) : errorAuditoria ? (
              <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">
                {errorAuditoria}
              </div>
            ) : auditoria.length === 0 ? (
              <div className="py-16 text-center text-sm text-muted-foreground">
                No existen eventos de auditoría para este paciente.
              </div>
            ) : (
              <div className="space-y-3 pb-2">
                {auditoria.map((evento) => {
                  const accion = String(v(evento, "accion") || "").toUpperCase();
                  const anulacion = accion === "ANULACION";
                  const datos = jsonSeguro(
                    anulacion
                      ? v(evento, "datosAnteriores", "datos_anteriores")
                      : v(evento, "datosNuevos", "datos_nuevos")
                  );

                  return (
                    <div
                      key={v(evento, "id")}
                      className={`rounded-lg border p-4 ${
                        anulacion ? "border-red-200 bg-red-50/40" : "bg-background"
                      }`}
                    >
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <div className="flex items-center gap-2">
                            <span className="font-semibold">
                              {v(evento, "vacuna") || `Vacuna ${v(evento, "vacunaId", "vacuna_id")}`}
                            </span>
                            <span className="text-sm text-muted-foreground">
                              · dosis {v(evento, "numeroDosis", "numero_dosis")}
                            </span>
                          </div>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {v(evento, "claveAnx", "clave_anx") || "—"} · {fechaHora(v(evento, "fecha"))}
                          </div>
                        </div>

                        <Badge
                          variant={anulacion ? "destructive" : "secondary"}
                          className={!anulacion ? "bg-emerald-100 text-emerald-700" : ""}
                        >
                          {anulacion ? "ANULACIÓN" : "APLICACIÓN"}
                        </Badge>
                      </div>

                      <div className="mt-4 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
                        <div>
                          <span className="text-muted-foreground">Usuario: </span>
                          <strong>{v(evento, "usuarioNombre", "usuario_nombre") || "—"}</strong>
                        </div>
                        <div>
                          <span className="text-muted-foreground">Rol: </span>
                          {v(evento, "usuarioRol", "usuario_rol") || "—"}
                        </div>
                        <div>
                          <span className="text-muted-foreground">CLUES: </span>
                          <span className="font-mono text-xs">
                            {v(evento, "unidadMedicaClues", "unidad_medica_clues") || "—"}
                          </span>
                        </div>
                        <div>
                          <span className="text-muted-foreground">IP: </span>
                          <span className="font-mono text-xs">{v(evento, "ipOrigen", "ip_origen") || "—"}</span>
                        </div>

                        {!anulacion && (
                          <>
                            <div>
                              <span className="text-muted-foreground">Lote: </span>
                              <strong>{datos.lote || "—"}</strong>
                            </div>
                            <div>
                              <span className="text-muted-foreground">Laboratorio: </span>
                              {datos.laboratorio || "—"}
                            </div>
                            {datos.observaciones && (
                              <div className="sm:col-span-2">
                                <span className="text-muted-foreground">Observaciones: </span>
                                {datos.observaciones}
                              </div>
                            )}
                          </>
                        )}

                        {anulacion && (
                          <div className="sm:col-span-2 rounded-md border border-red-200 bg-white/70 p-3">
                            <span className="text-muted-foreground">Motivo de anulación: </span>
                            <strong>
                              {v(evento, "motivo") ||
                                v(evento, "motivoAnulacion", "motivo_anulacion") ||
                                "—"}
                            </strong>
                          </div>
                        )}
                      </div>

                      <div className="mt-3 text-[11px] text-muted-foreground">
                        Aplicación ID:{" "}
                        <span className="font-mono">
                          {v(evento, "aplicacionId", "aplicacion_id") || "—"}
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setAuditoriaOpen(false)}>
              Cerrar
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={anulacionOpen}
        onOpenChange={(open) => {
          if (!open) cerrarAnulacion();
        }}
      >
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Anular o corregir aplicación</DialogTitle>
            <DialogDescription>
              La aplicación no se eliminará. Quedará marcada como anulada y se registrará
              la operación en la auditoría de vacunación.
            </DialogDescription>
          </DialogHeader>

          {aplicacionAnular && (
            <div className="space-y-4">
              <div className="rounded-lg border bg-muted/30 p-4">
                <div className="font-medium">{v(aplicacionAnular, "vacuna")}</div>
                <div className="mt-1 text-sm text-muted-foreground">
                  Dosis {v(aplicacionAnular, "numeroDosis", "numero_dosis")}
                  {" · "}
                  {fechaCorta(v(aplicacionAnular, "fechaAplicacion", "fecha_aplicacion"))}
                </div>
                <div className="mt-1 text-xs text-muted-foreground">
                  Lote: {v(aplicacionAnular, "lote") || "—"}
                  {" · "}
                  CLUES: {v(aplicacionAnular, "unidadMedicaClues", "unidad_medica_clues") || "—"}
                </div>
              </div>

              <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                Al confirmar, esta dosis dejará de contar inmediatamente para el cálculo
                del esquema. El motor volverá a evaluar al paciente.
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="motivo-anulacion">Motivo de la anulación *</Label>
                <textarea
                  id="motivo-anulacion"
                  value={motivoAnulacion}
                  onChange={(e) => setMotivoAnulacion(e.target.value)}
                  maxLength={500}
                  rows={4}
                  disabled={anulando}
                  placeholder="Ej. lote capturado incorrectamente, fecha errónea, aplicación registrada al paciente equivocado..."
                  className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                />
                <div className="text-right text-xs text-muted-foreground">
                  {motivoAnulacion.trim().length}/500
                </div>
              </div>

              <div className="rounded-md bg-muted/40 p-3 text-xs text-muted-foreground">
                <strong>Anular</strong> conserva el registro sólo como antecedente auditado.
                <strong> Anular y corregir</strong> además intentará preparar nuevamente la
                misma vacuna y dosis si el motor la considera aplicable después del recálculo.
              </div>
            </div>
          )}

          <DialogFooter className="gap-2 sm:gap-0">
            <Button
              type="button"
              variant="outline"
              onClick={cerrarAnulacion}
              disabled={anulando}
            >
              Cancelar
            </Button>

            <Button
              type="button"
              variant="destructive"
              onClick={() => anularAplicacion(false)}
              disabled={anulando || motivoAnulacion.trim().length < 5}
            >
              {anulando ? (
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
              ) : (
                <Ban className="h-4 w-4 mr-2" />
              )}
              Anular
            </Button>

            <Button
              type="button"
              onClick={() => anularAplicacion(true)}
              disabled={anulando || motivoAnulacion.trim().length < 5}
            >
              {anulando ? (
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
              ) : (
                <RotateCcw className="h-4 w-4 mr-2" />
              )}
              Anular y corregir
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <div className="flex items-start gap-2 text-xs text-muted-foreground">
        <AlertCircle className="h-4 w-4 mt-0.5 shrink-0" />
        <span>
          Esta pantalla no decide el esquema en React. Las dosis aplicables son calculadas y validadas nuevamente por SNCEV.Api al registrar la aplicación.
        </span>
      </div>
    </div>
  );
}
