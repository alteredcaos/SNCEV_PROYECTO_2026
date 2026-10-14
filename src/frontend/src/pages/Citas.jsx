import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";

import {
  CalendarDays,
  CheckCircle2,
  Clock,
  MapPin,
  RefreshCw,
  ShieldAlert,
  Syringe,
  XCircle,
} from "lucide-react";

import apiClient from "@/api/apiClient";

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

import { useToast } from "@/components/ui/use-toast";


// ============================================================
// Helpers
// ============================================================

const value = (obj, camel, snake) => {
  if (!obj) return undefined;

  const pascal =
    camel.charAt(0).toUpperCase() + camel.slice(1);

  return obj[camel] ?? obj[pascal] ?? obj[snake];
};


const fechaLocal = (fecha) => {
  if (!fecha) return "—";

  const d = new Date(fecha);

  if (Number.isNaN(d.getTime()))
    return fecha;

  return d.toLocaleDateString("es-MX", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
};


const fechaHoraLocal = (fecha) => {
  if (!fecha) return "—";

  const d = new Date(fecha);

  if (Number.isNaN(d.getTime()))
    return fecha;

  return d.toLocaleString("es-MX", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
};


const hoyIso = () => {
  const d = new Date();

  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");

  return `${y}-${m}-${day}`;
};


const sumarDiasIso = (dias) => {
  const d = new Date();
  d.setDate(d.getDate() + dias);

  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");

  return `${y}-${m}-${day}`;
};


const estadoBadge = (estado) => {
  switch (estado) {
    case "APLICABLE":
      return "default";

    case "FUTURA":
      return "secondary";

    case "BLOQUEADA":
      return "outline";

    case "VERIFICAR":
      return "destructive";

    default:
      return "outline";
  }
};


const estadoCitaBadge = (estado) => {
  switch (estado) {
    case "PROGRAMADA":
      return "secondary";

    case "CONFIRMADA":
      return "default";

    case "ATENDIDA":
      return "default";

    case "NO_ACUDIO":
      return "destructive";

    case "CANCELADA":
      return "outline";

    default:
      return "outline";
  }
};


// ============================================================
// Componente
// ============================================================

export default function Citas() {
  const { toast } = useToast();
  const navigate = useNavigate();

  // ----------------------------------------------------------
  // Oportunidades
  // ----------------------------------------------------------

  const [oportunidades, setOportunidades] = useState([]);
  const [resumen, setResumen] = useState({
    sinCita: 0,
    conCita: 0,
    aplicables: 0,
    futuras: 0,
    bloqueadas: 0,
    verificar: 0,
  });

  const [estadoFiltro, setEstadoFiltro] =
    useState("APLICABLE");

  const [conCitaFiltro, setConCitaFiltro] =
    useState("TODAS");

  const [cargandoOportunidades, setCargandoOportunidades] =
    useState(false);


  // ----------------------------------------------------------
  // Agenda
  // ----------------------------------------------------------

  const [citas, setCitas] = useState([]);
  const [cargandoCitas, setCargandoCitas] =
    useState(false);

  const [desde, setDesde] = useState(hoyIso());
  const [hasta, setHasta] = useState(sumarDiasIso(30));


  // ----------------------------------------------------------
  // Formulario de cita
  // ----------------------------------------------------------

  const [seleccionada, setSeleccionada] =
    useState(null);

  const [fecha, setFecha] = useState("");
  const [hora, setHora] = useState("10:00");
  const [notas, setNotas] = useState("");

  const [guardando, setGuardando] =
    useState(false);


  // ==========================================================
  // Cargar oportunidades
  // ==========================================================

  const cargarOportunidades = useCallback(async () => {
    setCargandoOportunidades(true);

    try {
      const params = new URLSearchParams();

      params.set("page", "1");
      params.set("pageSize", "50");

      if (estadoFiltro !== "TODAS") {
        params.set("estado", estadoFiltro);
      }

      if (conCitaFiltro === "SI") {
        params.set("conCita", "true");
      }

      if (conCitaFiltro === "NO") {
        params.set("conCita", "false");
      }

      const data = await apiClient.get(
        `/oportunidades-vacunacion?${params.toString()}`
      );

      setOportunidades(
        value(data, "items", "items") ?? []
      );

      setResumen({
        sinCita:
          value(
            value(data, "resumen", "resumen"),
            "sinCita",
            "sin_cita"
          ) ?? 0,

        conCita:
          value(
            value(data, "resumen", "resumen"),
            "conCita",
            "con_cita"
          ) ?? 0,

        aplicables:
          value(
            value(data, "resumen", "resumen"),
            "aplicables",
            "aplicables"
          ) ?? 0,

        futuras:
          value(
            value(data, "resumen", "resumen"),
            "futuras",
            "futuras"
          ) ?? 0,

        bloqueadas:
          value(
            value(data, "resumen", "resumen"),
            "bloqueadas",
            "bloqueadas"
          ) ?? 0,

        verificar:
          value(
            value(data, "resumen", "resumen"),
            "verificar",
            "verificar"
          ) ?? 0,
      });
    } catch (error) {
      toast({
        title: "No fue posible cargar oportunidades",
        description: error.message,
        variant: "destructive",
      });
    } finally {
      setCargandoOportunidades(false);
    }
  }, [
    estadoFiltro,
    conCitaFiltro,
    toast,
  ]);


  // ==========================================================
  // Cargar agenda
  // ==========================================================

  const cargarCitas = useCallback(async () => {
    setCargandoCitas(true);

    try {
      const params = new URLSearchParams();

      if (desde)
        params.set("desde", desde);

      if (hasta)
        params.set("hasta", hasta);

      const data = await apiClient.get(
        `/citas?${params.toString()}`
      );

      setCitas(
        value(data, "items", "items") ?? []
      );
    } catch (error) {
      toast({
        title: "No fue posible cargar la agenda",
        description: error.message,
        variant: "destructive",
      });
    } finally {
      setCargandoCitas(false);
    }
  }, [desde, hasta, toast]);


  useEffect(() => {
    cargarOportunidades();
  }, [cargarOportunidades]);


  useEffect(() => {
    cargarCitas();
  }, [cargarCitas]);


  // ==========================================================
  // Seleccionar oportunidad
  // ==========================================================

  const seleccionarOportunidad = (o) => {
  
    const estado =
      value(o, "estadoOportunidad", "estado_oportunidad");

    const tieneCita =
      value(o, "tieneCitaActiva", "tiene_cita_activa");

    if (tieneCita) {
      toast({
        title: "La oportunidad ya tiene cita",
        description:
          "No se puede crear otra cita activa para la misma vacuna y dosis.",
      });

      return;
    }

    if (estado !== "APLICABLE" &&
        estado !== "FUTURA") {
      toast({
        title: "La oportunidad no es agendable",
        description:
          estado === "VERIFICAR"
            ? "Debe verificarse antes de programar una cita."
            : "La dosis está bloqueada por una condición previa.",
        variant: "destructive",
      });

      return;
    }

    setSeleccionada(o);

    const minima =
      value(
        o,
        "fechaMinimaAplicacion",
        "fecha_minima_aplicacion"
      );

    if (minima) {
      setFecha(minima.substring(0, 10));
    } else {
      setFecha(sumarDiasIso(1));
    }

    setHora("10:00");
    setNotas("");
  };


  // ==========================================================
  // Crear cita
  // ==========================================================

  const crearCita = async () => {
    if (!seleccionada)
      return;

    if (!fecha || !hora) {
      toast({
        title: "Fecha y hora requeridas",
        variant: "destructive",
      });

      return;
    }

    const pacienteId =
      value(
        seleccionada,
        "pacienteId",
        "paciente_id"
      );

    const vacunaId =
      value(
        seleccionada,
        "vacunaId",
        "vacuna_id"
      );

    const numeroDosis =
      value(
        seleccionada,
        "numeroDosis",
        "numero_dosis"
      );

    setGuardando(true);

    try {
      const creada = await apiClient.post(
        "/citas",
        {
          pacienteId,
          vacunaId,
          numeroDosis,
          fechaHora: `${fecha}T${hora}:00`,
          notas: notas.trim() || null,
        }
      );

      toast({
        title: "Cita programada",
        description:
          `${value(seleccionada, "vacuna", "vacuna")} ` +
          `D${numeroDosis} · ${fecha} ${hora}`,
      });

      setSeleccionada(null);
      setFecha("");
      setHora("10:00");
      setNotas("");

      await Promise.all([
        cargarOportunidades(),
        cargarCitas(),
      ]);

      return creada;
    } catch (error) {
      if (error.status === 409) {
        toast({
          title: "Ya existe una cita activa",
          description:
            error.data?.message ??
            "El paciente ya tiene una cita activa para esta vacuna y dosis.",
          variant: "destructive",
        });

        await Promise.all([
          cargarOportunidades(),
          cargarCitas(),
        ]);

        return;
      }

      toast({
        title: "No fue posible programar la cita",
        description: error.message,
        variant: "destructive",
      });
    } finally {
      setGuardando(false);
    }
  };


  // ==========================================================
  // Cambiar estado de cita
  // ==========================================================

  const cambiarEstado = async (cita, nuevoEstado) => {
    const id = value(cita, "id", "id");

    if (!id)
      return;

    try {
      await apiClient.patch(
        `/citas/${id}/estado`,
        {
          estado: nuevoEstado,
        }
      );

      toast({
        title: "Cita actualizada",
        description: `Nuevo estado: ${nuevoEstado}`,
      });

      await Promise.all([
        cargarCitas(),
        cargarOportunidades(),
      ]);
    } catch (error) {
      toast({
        title: "No fue posible actualizar la cita",
        description: error.message,
        variant: "destructive",
      });
    }
  };


  // ==========================================================
  // Agrupar agenda por fecha
  // ==========================================================

  const citasAgrupadas = useMemo(() => {
    const grupos = {};

    for (const cita of citas) {
      const fechaHora =
        value(cita, "fechaHora", "fecha_hora");

      if (!fechaHora)
        continue;

      const clave = fechaHora.substring(0, 10);

      if (!grupos[clave])
        grupos[clave] = [];

      grupos[clave].push(cita);
    }

    for (const grupo of Object.values(grupos)) {
      grupo.sort((a, b) => {
        const fa =
          value(a, "fechaHora", "fecha_hora");

        const fb =
          value(b, "fechaHora", "fecha_hora");

        return new Date(fa) - new Date(fb);
      });
    }

    return grupos;
  }, [citas]);


  const fechasAgenda =
    Object.keys(citasAgrupadas).sort();


  // ==========================================================
  // Render
  // ==========================================================

  return (
    <div className="space-y-6">

      {/* =====================================================
          Encabezado
      ====================================================== */}

      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-heading font-semibold">
            Oportunidades y Agenda
          </h1>

          <p className="text-sm text-muted-foreground mt-1">
            Gestión de oportunidades vacunales y citas programadas
          </p>
        </div>

        <Button
          variant="outline"
          onClick={() =>
            Promise.all([
              cargarOportunidades(),
              cargarCitas(),
            ])
          }
        >
          <RefreshCw className="w-4 h-4 mr-2" />
          Actualizar
        </Button>
      </div>


      {/* =====================================================
          Resumen
      ====================================================== */}

      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">

        <ResumenCard
          titulo="Aplicables"
          valor={resumen.aplicables}
        />

        <ResumenCard
          titulo="Futuras"
          valor={resumen.futuras}
        />

        <ResumenCard
          titulo="Bloqueadas"
          valor={resumen.bloqueadas}
        />

        <ResumenCard
          titulo="Verificar"
          valor={resumen.verificar}
        />

        <ResumenCard
          titulo="Sin cita"
          valor={resumen.sinCita}
        />

        <ResumenCard
          titulo="Con cita"
          valor={resumen.conCita}
        />

      </div>


      {/* =====================================================
          Oportunidades
      ====================================================== */}

      <Card>
        <CardHeader>
          <CardTitle>
            Oportunidades de vacunación
          </CardTitle>

          <CardDescription>
            Calculadas dinámicamente por el motor de esquema.
            No constituyen registros almacenados.
          </CardDescription>
        </CardHeader>

        <CardContent className="space-y-4">

          {/* Filtros */}

          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-3">

            <div className="space-y-1.5">
              <Label>Estado</Label>

              <Select
                value={estadoFiltro}
                onValueChange={setEstadoFiltro}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>

                <SelectContent>
                  <SelectItem value="TODAS">
                    Todas
                  </SelectItem>

                  <SelectItem value="APLICABLE">
                    Aplicables ahora
                  </SelectItem>

                  <SelectItem value="FUTURA">
                    Futuras
                  </SelectItem>

                  <SelectItem value="BLOQUEADA">
                    Bloqueadas
                  </SelectItem>

                  <SelectItem value="VERIFICAR">
                    Requieren verificación
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>


            <div className="space-y-1.5">
              <Label>Cita activa</Label>

              <Select
                value={conCitaFiltro}
                onValueChange={setConCitaFiltro}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>

                <SelectContent>
                  <SelectItem value="TODAS">
                    Todas
                  </SelectItem>

                  <SelectItem value="NO">
                    Sin cita
                  </SelectItem>

                  <SelectItem value="SI">
                    Con cita
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>

          </div>


          {cargandoOportunidades ? (
            <div className="py-8 text-center text-muted-foreground">
              Cargando oportunidades…
            </div>
          ) : oportunidades.length === 0 ? (
            <div className="py-8 text-center text-muted-foreground">
              No hay oportunidades con los filtros seleccionados.
            </div>
          ) : (
            <div className="max-h-[520px] overflow-y-auto overscroll-contain pr-2 space-y-3">

              {oportunidades.map((o, index) => {
                const estado =
                  value(
                    o,
                    "estadoOportunidad",
                    "estado_oportunidad"
                  );

                const tieneCita =
                  value(
                    o,
                    "tieneCitaActiva",
                    "tiene_cita_activa"
                  );

                const cita =
                  value(o, "cita", "cita");

                const pacienteId =
                  value(o, "pacienteId", "paciente_id");

                const vacunaId =
                  value(o, "vacunaId", "vacuna_id");

                const dosis =
                  value(o, "numeroDosis", "numero_dosis");

                const puedeAgendar =
                  !tieneCita &&
                  dosis > 0 &&
                  (
                    estado === "APLICABLE" ||
                    estado === "FUTURA"
                  );

                return (
                  <div
                    key={`${pacienteId}-${vacunaId}-${dosis}-${estado}-${index}`}
                    className="border rounded-lg p-4"
                  >
                    <div className="flex items-start justify-between gap-4 flex-wrap">

                      <div className="space-y-2">

                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="font-medium">
                            {value(o, "paciente", "paciente")}
                          </span>

                          <Badge variant={estadoBadge(estado)}>
                            {estado}
                          </Badge>

                          {tieneCita && (
                            <Badge variant="secondary">
                              CON CITA
                            </Badge>
                          )}
                        </div>


                        <div className="flex items-center gap-2 text-sm">
                          <Syringe className="w-4 h-4" />

                          <span>
                            {value(o, "vacuna", "vacuna")}
                            {" · "}
                            Dosis {dosis}
                          </span>
                        </div>


                        {value(
                          o,
                          "fechaMinimaAplicacion",
                          "fecha_minima_aplicacion"
                        ) && (
                          <div className="flex items-center gap-2 text-sm text-muted-foreground">
                            <CalendarDays className="w-4 h-4" />

                            Desde{" "}
                            {fechaLocal(
                              value(
                                o,
                                "fechaMinimaAplicacion",
                                "fecha_minima_aplicacion"
                              )
                            )}
                          </div>
                        )}


                        <div className="text-sm text-muted-foreground">
                          {value(o, "motivo", "motivo")}
                        </div>


                        {(value(o, "agebClave", "ageb_clave") ||
                          value(o, "municipio", "municipio")) && (
                          <div className="flex items-center gap-2 text-xs text-muted-foreground">
                            <MapPin className="w-3.5 h-3.5" />

                            <span>
                              {value(o, "municipio", "municipio") || "—"}

                              {value(o, "agebClave", "ageb_clave")
                                ? ` · AGEB ${value(
                                    o,
                                    "agebClave",
                                    "ageb_clave"
                                  )}`
                                : ""}
                            </span>
                          </div>
                        )}


                        {cita && (
                          <div className="text-sm border rounded-md p-2 bg-muted/30">
                            <div className="flex items-center gap-2">
                              <Clock className="w-4 h-4" />

                              {fechaHoraLocal(
                                value(
                                  cita,
                                  "fechaHora",
                                  "fecha_hora"
                                )
                              )}

                              <Badge
                                variant={estadoCitaBadge(
                                  value(cita, "estado", "estado")
                                )}
                              >
                                {value(cita, "estado", "estado")}
                              </Badge>
                            </div>

                            <div className="text-xs text-muted-foreground mt-1">
                              CLUES:{" "}
                              {value(cita, "clues", "clues")}
                            </div>
                          </div>
                        )}

                      </div>


                      <div>
  {puedeAgendar && (
    <Button
      type="button"
      size="sm"
      onClick={() => seleccionarOportunidad(o)}
    >
      <CalendarDays className="w-4 h-4 mr-2" />
      Agendar
    </Button>
  )}

  {estado === "VERIFICAR" && (
    <div className="flex items-center gap-1 text-sm text-muted-foreground">
      <ShieldAlert className="w-4 h-4" />
      Revisión requerida
    </div>
  )}
</div>

</div>


{/* =====================================================
    Formulario inline de programación
====================================================== */}

{seleccionada &&
  value(seleccionada, "pacienteId", "paciente_id") === pacienteId &&
  value(seleccionada, "vacunaId", "vacuna_id") === vacunaId &&
  value(seleccionada, "numeroDosis", "numero_dosis") === dosis && (

    <div className="mt-4 pt-4 border-t">

      <div className="mb-4">
        <div className="font-medium">
          Programar cita
        </div>

        <div className="text-sm text-muted-foreground">
          {value(o, "vacuna", "vacuna")}
          {" · Dosis "}
          {dosis}
        </div>
      </div>


      <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-4">

        {/* FECHA */}
        <div className="space-y-1.5">
          <Label>Fecha</Label>

          <Input
            type="date"
            value={fecha}
            onChange={(e) =>
              setFecha(e.target.value)
            }
          />
        </div>


        {/* HORA */}
        <div className="space-y-1.5">
          <Label>Hora</Label>

          <Input
            type="time"
            value={hora}
            onChange={(e) =>
              setHora(e.target.value)
            }
          />
        </div>


        {/* NOTAS */}
        <div className="space-y-1.5 sm:col-span-2">
          <Label>Notas</Label>

          <Input
            value={notas}
            onChange={(e) =>
              setNotas(e.target.value)
            }
            placeholder="Opcional"
            maxLength={1000}
          />
        </div>


        {/* BOTONES */}
        <div className="sm:col-span-2 lg:col-span-4 flex justify-end gap-2">

          <Button
            type="button"
            variant="outline"
            onClick={() => {
              setSeleccionada(null);
              setFecha("");
              setHora("10:00");
              setNotas("");
            }}
            disabled={guardando}
          >
            Cancelar
          </Button>

          <Button
            type="button"
            onClick={crearCita}
            disabled={guardando}
          >
            <CalendarDays className="w-4 h-4 mr-2" />

            {guardando
              ? "Programando…"
              : "Confirmar cita"}
          </Button>

        </div>

      </div>
    </div>
)}

</div>
                );
              })}

            </div>
          )}

        </CardContent>
      </Card>


      {/* =====================================================
          Programar cita seleccionada
      ====================================================== */}

      {seleccionada && (
        <Card>
          <CardHeader>
            <CardTitle>
              Programar cita
            </CardTitle>

            <CardDescription>
              {value(
                seleccionada,
                "paciente",
                "paciente"
              )}
              {" · "}
              {value(
                seleccionada,
                "vacuna",
                "vacuna"
              )}
              {" · Dosis "}
              {value(
                seleccionada,
                "numeroDosis",
                "numero_dosis"
              )}
            </CardDescription>
          </CardHeader>

          <CardContent className="grid sm:grid-cols-2 lg:grid-cols-4 gap-4">

            <div className="space-y-1.5">
              <Label>Fecha</Label>

              <Input
                type="date"
                value={fecha}
                onChange={(e) =>
                  setFecha(e.target.value)
                }
              />
            </div>


            <div className="space-y-1.5">
              <Label>Hora</Label>

              <Input
                type="time"
                value={hora}
                onChange={(e) =>
                  setHora(e.target.value)
                }
              />
            </div>


            <div className="space-y-1.5 sm:col-span-2">
              <Label>Notas</Label>

              <Input
                value={notas}
                onChange={(e) =>
                  setNotas(e.target.value)
                }
                placeholder="Opcional"
                maxLength={1000}
              />
            </div>


            <div className="sm:col-span-2 lg:col-span-4 flex gap-2 justify-end">
              <Button
                variant="outline"
                onClick={() =>
                  setSeleccionada(null)
                }
                disabled={guardando}
              >
                Cancelar
              </Button>

              <Button
                onClick={crearCita}
                disabled={guardando}
              >
                <CalendarDays className="w-4 h-4 mr-2" />

                {guardando
                  ? "Programando…"
                  : "Confirmar cita"}
              </Button>
            </div>

          </CardContent>
        </Card>
      )}


      {/* =====================================================
          Agenda
      ====================================================== */}

      <Card>
        <CardHeader>
          <CardTitle>
            Agenda
          </CardTitle>

          <CardDescription>
            Citas registradas en SNCEV
          </CardDescription>
        </CardHeader>

        <CardContent className="space-y-5">

          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-3">

            <div className="space-y-1.5">
              <Label>Desde</Label>

              <Input
                type="date"
                value={desde}
                onChange={(e) =>
                  setDesde(e.target.value)
                }
              />
            </div>


            <div className="space-y-1.5">
              <Label>Hasta</Label>

              <Input
                type="date"
                value={hasta}
                onChange={(e) =>
                  setHasta(e.target.value)
                }
              />
            </div>

          </div>


          {cargandoCitas ? (
            <div className="py-8 text-center text-muted-foreground">
              Cargando agenda…
            </div>
          ) : fechasAgenda.length === 0 ? (
            <div className="py-8 text-center text-muted-foreground">
              No existen citas en el periodo seleccionado.
            </div>
          ) : (
            <div className="max-h-[520px] overflow-y-auto overscroll-contain pr-2 space-y-6">

              {fechasAgenda.map((fechaAgenda) => (
                <div key={fechaAgenda}>

                  <div className="flex items-center gap-2 mb-3">
                    <CalendarDays className="w-5 h-5" />

                    <h3 className="font-semibold">
                      {fechaLocal(
                        `${fechaAgenda}T12:00:00`
                      )}
                    </h3>

                    <Badge variant="outline">
                      {
                        citasAgrupadas[
                          fechaAgenda
                        ].length
                      }
                    </Badge>
                  </div>


                  <div className="space-y-2">

                    {citasAgrupadas[
                      fechaAgenda
                    ].map((cita) => {
                      const estado =
                        value(
                          cita,
                          "estado",
                          "estado"
                        );

                      return (
                        <div
                          key={value(cita, "id", "id")}
                          className="border rounded-lg p-4"
                        >
                          <div className="flex items-start justify-between gap-4 flex-wrap">

                            <div className="space-y-1.5">

                              <div className="flex items-center gap-2 flex-wrap">
                                <span className="font-medium">
                                  {value(
                                    cita,
                                    "paciente",
                                    "paciente"
                                  )}
                                </span>

                                <Badge
                                  variant={estadoCitaBadge(
                                    estado
                                  )}
                                >
                                  {estado}
                                </Badge>
                              </div>


                              <div className="text-sm flex items-center gap-2">
                                <Clock className="w-4 h-4" />

                                {fechaHoraLocal(
                                  value(
                                    cita,
                                    "fechaHora",
                                    "fecha_hora"
                                  )
                                )}
                              </div>


                              <div className="text-sm flex items-center gap-2">
                                <Syringe className="w-4 h-4" />

                                {value(
                                  cita,
                                  "vacuna",
                                  "vacuna"
                                )}
                                {" · Dosis "}
                                {value(
                                  cita,
                                  "numeroDosis",
                                  "numero_dosis"
                                )}
                              </div>


                              <div className="text-xs text-muted-foreground">
                                CLUES:{" "}
                                {value(
                                  cita,
                                  "clues",
                                  "clues"
                                )}
                              </div>

                            </div>


                            <div className="flex gap-2 flex-wrap">

                              {estado === "PROGRAMADA" && (
                                <Button
                                  size="sm"
                                  variant="outline"
                                  onClick={() =>
                                    cambiarEstado(
                                      cita,
                                      "CONFIRMADA"
                                    )
                                  }
                                >
                                  <CheckCircle2 className="w-4 h-4 mr-1" />
                                  Confirmar
                                </Button>
                              )}


                              {(estado === "PROGRAMADA" ||
                                estado === "CONFIRMADA") && (
                                <>
                                  <Button
                                    type="button"
                                    size="sm"
                                    variant="outline"
                                    onClick={() => {
                                      const pacienteId = value(cita, "pacienteId", "paciente_id");
                                      const vacunaId = value(cita, "vacunaId", "vacuna_id");
                                      const numeroDosis = value(cita, "numeroDosis", "numero_dosis");

                                      navigate(
                                        `/vacunacion?pacienteId=${encodeURIComponent(pacienteId)}` +
                                        `&vacunaId=${encodeURIComponent(vacunaId)}` +
                                        `&dosis=${encodeURIComponent(numeroDosis)}`
                                      );
                                    }}
                                  >
                                    <Syringe className="w-4 h-4 mr-1" />
                                    Aplicar vacuna
                                  </Button>

                                  <Button
                                    size="sm"
                                    variant="outline"
                                    onClick={() =>
                                      cambiarEstado(
                                        cita,
                                        "NO_ACUDIO"
                                      )
                                    }
                                  >
                                    No acudió
                                  </Button>

                                  <Button
                                    size="sm"
                                    variant="destructive"
                                    onClick={() =>
                                      cambiarEstado(
                                        cita,
                                        "CANCELADA"
                                      )
                                    }
                                  >
                                    <XCircle className="w-4 h-4 mr-1" />
                                    Cancelar
                                  </Button>
                                </>
                              )}

                            </div>

                          </div>
                        </div>
                      );
                    })}

                  </div>
                </div>
              ))}

            </div>
          )}

        </CardContent>
      </Card>

    </div>
  );
}


// ============================================================
// Tarjeta resumen
// ============================================================

function ResumenCard({ titulo, valor }) {
  return (
    <Card>
      <CardContent className="pt-4 pb-4">
        <div className="text-2xl font-semibold">
          {valor}
        </div>

        <div className="text-xs text-muted-foreground">
          {titulo}
        </div>
      </CardContent>
    </Card>
  );
}