import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Input } from "@/components/ui/input";
import { Link } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";

import {
  QrCode, Download, ShieldCheck, CheckCircle2, Clock, ArrowLeft,
  Loader2, AlertCircle, Search
} from "lucide-react";
import apiClient from "@/api/apiClient";

function v(obj, camel, snake = camel) {
  if (!obj) return undefined;
  const pascal = camel.charAt(0).toUpperCase() + camel.slice(1);
  return obj[camel] ?? obj[pascal] ?? obj[snake];
}

function arr(obj, camel, snake = camel) {
  const x = v(obj, camel, snake);
  return Array.isArray(x) ? x : [];
}

function nombreCompleto(p) {
  return [v(p, "nombre"), v(p, "primerApellido", "primer_apellido"), v(p, "segundoApellido", "segundo_apellido")]
    .filter(Boolean).join(" ");
}

function fechaCorta(fecha) {
  if (!fecha) return "—";
  const d = new Date(fecha);
  if (Number.isNaN(d.getTime())) return String(fecha);
  return d.toLocaleDateString("es-MX");
}

function edadTexto(fecha) {
  if (!fecha) return "—";
  const nacimiento = new Date(fecha);
  const hoy = new Date();
  if (Number.isNaN(nacimiento.getTime())) return "—";
  let meses = (hoy.getFullYear() - nacimiento.getFullYear()) * 12 +
    hoy.getMonth() - nacimiento.getMonth();
  if (hoy.getDate() < nacimiento.getDate()) meses--;
  meses = Math.max(0, meses);
  if (meses < 24) return `${meses} ${meses === 1 ? "mes" : "meses"}`;
  const anios = Math.floor(meses / 12);
  return `${anios} ${anios === 1 ? "año" : "años"}`;
}

function dosisTexto(item) {
  return v(item, "numeroDosis", "numero_dosis") ?? v(item, "dosis") ?? "—";
}

function vacunaTexto(item) {
  return v(item, "vacuna") ??
    v(item, "vacunaNombre", "vacuna_nombre") ??
    v(item, "nombreVacuna", "nombre_vacuna") ??
    v(item, "nombreGenerico", "nombre_generico") ??
    "Vacuna";
}

function estadoItem(estado) {
  if (estado === "APLICADA") {
    return { texto: "Aplicada", badge: "bg-emerald-100 text-emerald-700", icon: "bg-emerald-100 text-emerald-700" };
  }
  if (estado === "APLICABLE") {
    return { texto: "Aplicable ahora", badge: "bg-blue-100 text-blue-700", icon: "bg-blue-100 text-blue-700" };
  }
  if (estado === "PROGRAMADA") {
    return { texto: "Programada", badge: "bg-amber-100 text-amber-700", icon: "bg-amber-100 text-amber-700" };
  }
  return { texto: "Requiere verificación", badge: "bg-slate-100 text-slate-700", icon: "bg-slate-100 text-slate-700" };
}

export default function Cartilla() {
  const [q, setQ] = useState("");
const [pacientes, setPacientes] = useState([]);
const [totalPacientes, setTotalPacientes] = useState(0);

const [pacienteId, setPacienteId] = useState("");

const [expediente, setExpediente] = useState(null);
const [esquema, setEsquema] = useState(null);

const [cargandoPacientes, setCargandoPacientes] = useState(false);
const [errorPacientes, setErrorPacientes] = useState("");

const [cargandoCartilla, setCargandoCartilla] = useState(false);
const [error, setError] = useState("");
const [descargandoPdf, setDescargandoPdf] = useState(false);
const buscarPacientes = useCallback(async (texto) => {
  const termino = texto.trim();

  if (termino.length < 2) {
    setPacientes([]);
    setTotalPacientes(0);
    setErrorPacientes("");
    setCargandoPacientes(false);
    return;
  }

  setCargandoPacientes(true);
  setErrorPacientes("");

  try {
    const query = new URLSearchParams({
      page: "1",
      pageSize: "25",
      nombre: termino,
    });

    const data = await apiClient.get(`/pacientes?${query.toString()}`);
    setPacientes(arr(data, "items"));
    setTotalPacientes(Number(v(data, "total") || 0));
  } catch (e) {
    setPacientes([]);
    setTotalPacientes(0);
    setErrorPacientes(e.message || "No fue posible consultar pacientes.");
  } finally {
    setCargandoPacientes(false);
  }
}, []);

useEffect(() => {
  setPacienteId("");
  setExpediente(null);
  setEsquema(null);
  setError("");

  const termino = q.trim();

  if (termino.length < 2) {
    setPacientes([]);
    setTotalPacientes(0);
    setErrorPacientes("");
    setCargandoPacientes(false);
    return;
  }

  const timer = setTimeout(() => {
    buscarPacientes(termino);
  }, 300);

  return () => clearTimeout(timer);
}, [q, buscarPacientes]);

  useEffect(() => {
    if (!pacienteId) {
      setExpediente(null);
      setEsquema(null);
      return;
    }

    let activo = true;
    (async () => {
      setCargandoCartilla(true);
      setError("");
      try {
        const [exp, esq] = await Promise.all([
          apiClient.get(`/pacientes/${encodeURIComponent(pacienteId)}/expediente`),
          apiClient.get(`/pacientes/${encodeURIComponent(pacienteId)}/esquema-vacunacion`),
        ]);
        if (!activo) return;
        setExpediente(exp);
        setEsquema(esq);
      } catch (e) {
        if (activo) {
          setExpediente(null);
          setEsquema(null);
          setError(e.message || "No fue posible cargar la cartilla.");
        }
      } finally {
        if (activo) setCargandoCartilla(false);
      }
    })();

    return () => { activo = false; };
  }, [pacienteId]);

  const p = v(expediente, "paciente");
  const domicilio = v(expediente, "domicilioVigente", "domicilio_vigente");
  const aplicadas = arr(esquema, "aplicadas");
  const aplicables = arr(esquema, "aplicables");
  const programadas = arr(esquema, "programadas");
  const verificar = arr(esquema, "requierenVerificacion", "requieren_verificacion");

  const esquemaVisual = useMemo(() => [
    ...aplicadas.map((x) => ({ ...x, _estado: "APLICADA" })),
    ...aplicables.map((x) => ({ ...x, _estado: "APLICABLE" })),
    ...programadas.map((x) => ({ ...x, _estado: "PROGRAMADA" })),
    ...verificar.map((x) => ({ ...x, _estado: "VERIFICAR" })),
  ], [aplicadas, aplicables, programadas, verificar]);

  const total = esquemaVisual.length;
  const avance = total ? Math.round((aplicadas.length / total) * 100) : 0;
  const recomendacion = v(esquema, "recomendacion");
// ======================================================
// DESCARGAR CARTILLA PDF
// ======================================================
const descargarPdf = async () => {
  if (!pacienteId || descargandoPdf) return;

  setDescargandoPdf(true);
  setError("");

  try {
    const { blob, filename } = await apiClient.download(
      `/pacientes/${encodeURIComponent(pacienteId)}/cartilla/pdf`
    );

    const url = window.URL.createObjectURL(blob);

    const link = document.createElement("a");
    link.href = url;
    link.download = filename;

    document.body.appendChild(link);
    link.click();
    link.remove();

    window.setTimeout(() => {
      window.URL.revokeObjectURL(url);
    }, 1000);
  } catch (e) {
    setError(
      e.message ||
        "No fue posible generar la cartilla en PDF."
    );
  } finally {
    setDescargandoPdf(false);
  }
};
  const direccion = domicilio
    ? [
        v(domicilio, "calle"),
        v(domicilio, "numeroExterior", "numero_exterior"),
        v(domicilio, "numeroInterior", "numero_interior") ? `Int. ${v(domicilio, "numeroInterior", "numero_interior")}` : null,
        v(domicilio, "colonia"),
        v(domicilio, "localidad"),
        v(domicilio, "municipioNombre", "municipio_nombre"),
        v(domicilio, "estadoNombre", "estado_nombre"),
        v(domicilio, "codigoPostal", "codigo_postal") ? `C.P. ${v(domicilio, "codigoPostal", "codigo_postal")}` : null,
      ].filter(Boolean).join(", ")
    : "Sin domicilio vigente registrado";






  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-heading font-semibold">Cartilla electrónica de vacunación</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Información clínica obtenida del expediente y del motor de esquema SNCEV
          </p>
        </div>

        <div className="flex items-center gap-3">
      

          <Button
  variant="outline"
  onClick={descargarPdf}
  disabled={!pacienteId || !expediente || descargandoPdf}
  title={
    pacienteId
      ? "Descargar cartilla en PDF"
      : "Selecciona un paciente"
  }
>
  {descargandoPdf ? (
    <Loader2 className="w-4 h-4 mr-2 animate-spin" />
  ) : (
    <Download className="w-4 h-4 mr-2" />
  )}

  {descargandoPdf ? "Generando..." : "PDF"}
</Button>
        </div>
      </div>

      <Card>
  <CardHeader>
    <CardTitle>Seleccionar paciente</CardTitle>

    <CardDescription>
      Pacientes activos registrados en SNCEV
    </CardDescription>
  </CardHeader>

  <CardContent className="space-y-3">

    {/* BUSCADOR */}
    <div className="relative">
      <Search
        className="
          absolute
          left-3
          top-2.5
          h-4
          w-4
          text-muted-foreground
        "
      />

      <Input
        value={q}
        onChange={(e) => setQ(e.target.value)}
        placeholder="Nombre, apellidos o CURP..."
        className="pl-9"
      />
    </div>

    {/* CARGANDO */}
    {cargandoPacientes && (
      <div
        className="
          flex
          items-center
          gap-2
          py-3
          text-sm
          text-muted-foreground
        "
      >
        <Loader2
          className="h-4 w-4 animate-spin"
        />

        Consultando pacientes...
      </div>
    )}

    {/* ERROR */}
    {errorPacientes && (
      <div
        className="
          rounded-md
          border
          border-destructive/30
          bg-destructive/5
          p-3
          text-sm
          text-destructive
        "
      >
        {errorPacientes}
      </div>
    )}

    {/* RESULTADOS */}
    {!cargandoPacientes && !errorPacientes && q.trim().length < 2 && (
      <div className="py-4 text-center text-sm text-muted-foreground">
        Escribe al menos 2 caracteres para buscar un paciente.
      </div>
    )}

    {!cargandoPacientes &&
      !errorPacientes &&
      q.trim().length >= 2 && (
        <>
          <div
            className="
              text-xs
              text-muted-foreground
            "
          >
            {totalPacientes}{" "}
            {totalPacientes === 1
              ? "paciente encontrado"
              : "pacientes encontrados"}
          </div>

          <div
            className="
              max-h-[260px]
              overflow-y-auto
              overscroll-contain
              divide-y
            "
          >
            {pacientes.map((x) => {
              const id = String(v(x, "id"));

              const seleccionado =
                pacienteId === id;

              return (
                <button
                  type="button"
                  key={id}
                  onClick={() =>
                    setPacienteId(id)
                  }
                  className={`
                    w-full
                    text-left
                    py-3
                    px-2
                    rounded-md
                    transition
                    ${
                      seleccionado
                        ? "bg-muted"
                        : "hover:bg-muted/50"
                    }
                  `}
                >
                  <div
                    className="
                      font-medium
                      text-sm
                    "
                  >
                    {nombreCompleto(x)}
                  </div>

                  <div
                    className="
                      flex
                      items-center
                      justify-between
                      gap-2
                      mt-1
                    "
                  >
                    <div
                      className="
                        text-xs
                        text-muted-foreground
                      "
                    >
                      {edadTexto(
                        v(
                          x,
                          "fechaNacimiento",
                          "fecha_nacimiento"
                        )
                      )}

                      {v(x, "curp")
                        ? ` · ${v(x, "curp")}`
                        : ""}
                    </div>

                    {seleccionado && (
                      <Badge>
                        Seleccionado
                      </Badge>
                    )}
                  </div>
                </button>
              );
            })}

            {pacientes.length === 0 && (
              <div
                className="
                  py-6
                  text-center
                  text-sm
                  text-muted-foreground
                "
              >
                No se encontraron pacientes.
              </div>
            )}
          </div>
        </>
      )}
  </CardContent>
</Card>

      {error && (
        <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive flex gap-2">
          <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
          {error}
        </div>
      )}

      {cargandoCartilla && (
        <div className="py-20 flex items-center justify-center gap-2 text-muted-foreground">
          <Loader2 className="w-5 h-5 animate-spin" /> Cargando cartilla...
        </div>
      )}

      {!cargandoCartilla && p && (
        <div className="grid gap-6 lg:grid-cols-3">
          <Card className="lg:col-span-2">
            <CardContent className="p-0">
              <div className="bg-gradient-to-r from-primary to-primary/80 text-primary-foreground p-5 rounded-t-xl">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-xs uppercase tracking-widest opacity-80">Secretaría de Salud</p>
                    <p className="font-heading font-semibold text-lg">Cartilla Nacional de Vacunación</p>
                    <p className="text-xs opacity-80">
                      {v(p, "tipoCartilla", "tipo_cartilla") || v(p, "versionCartilla", "version_cartilla") || "Cartilla"}
                    </p>
                  </div>
                  <ShieldCheck className="w-9 h-9 opacity-90" />
                </div>
              </div>

              <div className="p-5 space-y-5">
                <div className="grid sm:grid-cols-2 gap-x-6 gap-y-2 text-sm">
                  <Campo label="Nombre completo" value={nombreCompleto(p)} />
                  <Campo label="CURP" value={v(p, "curp") || "—"} mono />
                  <Campo label="Fecha de nacimiento" value={fechaCorta(v(p, "fechaNacimiento", "fecha_nacimiento"))} />
                  <Campo label="Edad" value={edadTexto(v(p, "fechaNacimiento", "fecha_nacimiento"))} />
                  <Campo label="Sexo" value={v(p, "sexo") === "M" ? "Masculino" : v(p, "sexo") === "F" ? "Femenino" : "—"} />
                  <Campo label="Tipo de cartilla" value={v(p, "tipoCartilla", "tipo_cartilla") || v(p, "versionCartilla", "version_cartilla") || "—"} />
                </div>

                <Separator />

                <div>
                  <p className="text-sm font-medium mb-2">Domicilio vigente</p>
                  <p className="text-sm text-muted-foreground">{direccion}</p>
                  {domicilio && (
                    <div className="mt-2 flex flex-wrap gap-2">
                      {v(domicilio, "cvegeo") && <Badge variant="outline">AGEB {v(domicilio, "cvegeo")}</Badge>}
                      {v(domicilio, "cluesSugerida", "clues_sugerida") && (
                        <Badge variant="outline">CLUES cercana {v(domicilio, "cluesSugerida", "clues_sugerida")}</Badge>
                      )}
                    </div>
                  )}
                </div>

                <Separator />

                <div>
                  <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
                    <p className="text-sm font-medium">Esquema de vacunación</p>
                    <Badge variant="outline">{aplicadas.length} dosis aplicadas</Badge>
                  </div>

                  {recomendacion && (
  <div
    className="
      mb-4
      rounded-lg
      border
      bg-muted/30
      p-3
    "
  >
    <p
      className="
        text-xs
        uppercase
        tracking-wide
        text-muted-foreground
      "
    >
      Recomendación del motor
    </p>

    <p
      className="
        text-sm
        font-medium
        mt-1
      "
    >
      {v(
        recomendacion,
        "vacuna"
      ) || "Vacuna"}

      {" · dosis "}

      {v(
        recomendacion,
        "numeroDosis",
        "numero_dosis"
      ) ?? "—"}
    </p>

    {v(recomendacion, "motivo") && (
      <p
        className="
          text-xs
          text-muted-foreground
          mt-1
        "
      >
        {v(
          recomendacion,
          "motivo"
        )}
      </p>
    )}
  </div>
)}

                  <div className="space-y-2 max-h-[560px] overflow-y-auto overscroll-contain pr-1">
                    {esquemaVisual.length === 0 ? (
                      <div className="rounded-lg border p-6 text-center text-sm text-muted-foreground">
                        No hay dosis calculadas para mostrar.
                      </div>
                    ) : esquemaVisual.map((e, index) => {
                      const est = estadoItem(e._estado);
                      const aplicada = e._estado === "APLICADA";
                      const fecha = v(e, "fechaAplicacion", "fecha_aplicacion");
                      const lote = v(e, "lote");
                      const laboratorio = v(e, "laboratorio");
                      const clave = v(e, "claveAnx", "clave_anx") ?? v(e, "claveCensia", "clave_censia");
                      const key = `${e._estado}-${v(e, "vacunaId", "vacuna_id") ?? vacunaTexto(e)}-${dosisTexto(e)}-${index}`;

                      return (
                        <div key={key} className="flex items-start gap-3 rounded-lg border p-3">
                          <div className={`w-8 h-8 mt-0.5 rounded-full flex items-center justify-center shrink-0 ${est.icon}`}>
                            {aplicada ? <CheckCircle2 className="w-4 h-4" /> : <Clock className="w-4 h-4" />}
                          </div>

                          <div className="flex-1 min-w-0">
                            <p className="text-sm font-medium">
                              {vacunaTexto(e)}
                              <span className="text-muted-foreground"> · dosis {dosisTexto(e)}</span>
                            </p>
                            <p className="text-xs text-muted-foreground mt-1">
                              {aplicada
                                ? [
                                    fecha ? `Aplicada ${fechaCorta(fecha)}` : "Aplicada",
                                    lote ? `lote ${lote}` : null,
                                    laboratorio || null,
                                  ].filter(Boolean).join(" · ")
                                : e._estado === "APLICABLE"
                                  ? "Puede administrarse de acuerdo con el motor de esquema."
                                  : e._estado === "PROGRAMADA"
                                    ? "Programada de acuerdo con edad, intervalo y reglas del esquema."
                                    : "Requiere revisión antes de determinar su aplicación."}
                            </p>
                            {clave && <p className="text-[11px] text-muted-foreground mt-1">{clave}</p>}
                          </div>

                          <Badge variant="secondary" className={est.badge}>{est.texto}</Badge>
                        </div>
                      );
                    })}
                  </div>
                </div>
              </div>

              <div className="border-t p-4 flex items-center justify-between gap-3 bg-muted/30 rounded-b-xl">
                <div className="text-xs text-muted-foreground">
                  <p>Cartilla clínica electrónica SNCEV</p>
                  <p>Las aplicaciones anuladas se conservan en auditoría y no cuentan como dosis vigentes.</p>
                </div>
                <Badge variant="outline"><ShieldCheck className="w-3 h-3 mr-1" /> Datos del sistema</Badge>
              </div>
            </CardContent>
          </Card>

          <div className="space-y-6">
            <Card>
              <CardHeader><CardTitle className="text-base">Verificación pública</CardTitle></CardHeader>
              <CardContent className="flex flex-col items-center text-center">
                <div className="w-40 h-40 rounded-xl border-2 border-dashed flex items-center justify-center bg-muted/30">
                  <QrCode className="w-24 h-24 text-muted-foreground" />
                </div>
                <p className="text-xs text-muted-foreground mt-3">
                  QR pendiente de implementar. No se muestra un código ficticio.
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2"><CardDescription>Avance del esquema calculado</CardDescription></CardHeader>
              <CardContent>
                <p className="text-4xl font-heading font-semibold">{avance}%</p>
                <p className="text-sm text-muted-foreground mt-1">
                  {aplicadas.length} de {total} dosis/indicaciones mostradas
                </p>

                <div className="mt-4 space-y-1 text-xs text-muted-foreground">
                  <p>Aplicables ahora: {aplicables.length}</p>
                  <p>Programadas: {programadas.length}</p>
                  <p>Requieren verificación: {verificar.length}</p>
                </div>

                <Link to={`/pacientes/${v(p, "id")}`} className="mt-4 block">
                  <Button variant="outline" size="sm" className="w-full">
                    <ArrowLeft className="w-4 h-4 mr-2" /> Ver expediente
                  </Button>
                </Link>
              </CardContent>
            </Card>
          </div>
        </div>
      )}

      {!cargandoPacientes && !cargandoCartilla && !pacienteId && !error && (
        <div className="py-10 text-center text-muted-foreground">
          <Search className="w-8 h-8 mx-auto mb-3 opacity-50" />
          <p className="font-medium text-foreground">Selecciona un paciente</p>
          <p className="text-sm mt-1">
            Busca por nombre, apellidos o CURP y selecciona un paciente para consultar su cartilla electrónica.
          </p>
        </div>
      )}
    </div>
  );
}

function Campo({ label, value, mono }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={`font-medium ${mono ? "font-mono text-xs" : ""}`}>{value}</p>
    </div>
  );
}