import React, { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  ArrowLeft,
  AlertTriangle,
  Loader2,
  MapPin,
  User,
  ShieldCheck,
  FileClock,
} from "lucide-react";

import { apiClient } from "@/api/apiClient";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

const value = (obj, camel, snake) => {
  if (!obj) return null;
  const pascal = camel.charAt(0).toUpperCase() + camel.slice(1);
  return obj[camel] ?? obj[pascal] ?? obj[snake] ?? null;
};

function fechaMX(fecha) {
  if (!fecha) return "—";

  const d = new Date(fecha);
  if (Number.isNaN(d.getTime())) return String(fecha);

  return d.toLocaleDateString("es-MX");
}

function fechaHoraMX(fecha) {
  if (!fecha) return "—";

  const d = new Date(fecha);
  if (Number.isNaN(d.getTime())) return String(fecha);

  return d.toLocaleString("es-MX", {
    dateStyle: "short",
    timeStyle: "short",
  });
}

function calcularEdad(fechaNacimiento) {
  if (!fechaNacimiento) return "—";

  const nacimiento = new Date(fechaNacimiento);
  if (Number.isNaN(nacimiento.getTime())) return "—";

  const hoy = new Date();

  let edad = hoy.getFullYear() - nacimiento.getFullYear();

  const mes = hoy.getMonth() - nacimiento.getMonth();

  if (
    mes < 0 ||
    (mes === 0 && hoy.getDate() < nacimiento.getDate())
  ) {
    edad--;
  }

  return `${edad} años`;
}

function Campo({ label, children }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="font-medium mt-1">{children || "—"}</p>
    </div>
  );
}

export default function ExpedienteInactivo() {
  const { id } = useParams();

  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let activo = true;

    async function cargar() {
      setLoading(true);
      setError("");

      try {
        const result = await apiClient.get(
          `/pacientes/${encodeURIComponent(id)}/expediente-inactivo`
        );

        if (activo) {
          setData(result);
        }
      } catch (err) {
        if (activo) {
          setError(
            err.message ||
              "No fue posible consultar el expediente inactivo."
          );
        }
      } finally {
        if (activo) {
          setLoading(false);
        }
      }
    }

    cargar();

    return () => {
      activo = false;
    };
  }, [id]);

  if (loading) {
    return (
      <div className="min-h-[50vh] flex items-center justify-center">
        <div className="text-center text-muted-foreground">
          <Loader2 className="w-7 h-7 animate-spin mx-auto mb-3" />
          Cargando expediente inactivo…
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="space-y-4">
        <Button variant="outline" asChild>
          <Link to="/administracion">
            <ArrowLeft className="w-4 h-4 mr-2" />
            Volver a Administración
          </Link>
        </Button>

        <Card>
          <CardContent className="py-10 text-center">
            <AlertTriangle className="w-8 h-8 mx-auto mb-3 text-destructive" />
            <p className="font-medium">
              No fue posible consultar el expediente
            </p>
            <p className="text-sm text-muted-foreground mt-2">
              {error}
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  const paciente =
    value(data, "paciente", "paciente") || {};

  const auditoria =
    value(data, "auditoria", "auditoria") || [];

  const nombreCompleto = [
    value(paciente, "nombre", "nombre"),
    value(paciente, "primerApellido", "primer_apellido"),
    value(paciente, "segundoApellido", "segundo_apellido"),
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Button variant="outline" asChild>
          <Link to="/administracion">
            <ArrowLeft className="w-4 h-4 mr-2" />
            Volver a Administración
          </Link>
        </Button>

        <Badge variant="destructive" className="text-sm px-3 py-1">
          EXPEDIENTE INACTIVO
        </Badge>
      </div>

      <Card className="border-destructive/30">
        <CardContent className="py-4">
          <div className="flex gap-3">
            <AlertTriangle className="w-5 h-5 text-destructive shrink-0 mt-0.5" />

            <div>
              <p className="font-medium">
                Expediente dado de baja
              </p>

              <p className="text-sm text-muted-foreground mt-1">
                Esta información se muestra exclusivamente para
                consulta administrativa. La visualización del expediente
                no reactiva ni modifica al paciente.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      <div>
        <h1 className="text-2xl font-heading font-semibold">
          {nombreCompleto || "Expediente del paciente"}
        </h1>

        <p className="text-sm text-muted-foreground mt-1">
          Consulta administrativa de expediente inactivo
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <User className="w-5 h-5" />
              Datos del paciente
            </CardTitle>
          </CardHeader>

          <CardContent className="grid sm:grid-cols-2 gap-5">
            <Campo label="Nombre completo">
              {nombreCompleto}
            </Campo>

            <Campo label="Sexo">
              {value(paciente, "sexo", "sexo") === "M"
                ? "Masculino"
                : value(paciente, "sexo", "sexo") === "F"
                ? "Femenino"
                : "—"}
            </Campo>

            <Campo label="Fecha de nacimiento">
              {fechaMX(
                value(
                  paciente,
                  "fechaNacimiento",
                  "fecha_nacimiento"
                )
              )}
            </Campo>

            <Campo label="Edad">
              {calcularEdad(
                value(
                  paciente,
                  "fechaNacimiento",
                  "fecha_nacimiento"
                )
              )}
            </Campo>

            <Campo label="Entidad de nacimiento">
              {value(
                paciente,
                "entidadNacimiento",
                "entidad_nacimiento"
              )}
            </Campo>

            <Campo label="Cartilla">
              {value(
                paciente,
                "tipoCartilla",
                "tipo_cartilla"
              ) ||
                value(
                  paciente,
                  "versionCartilla",
                  "version_cartilla"
                )}
            </Campo>

            <Campo label="Pueblo indígena">
              {value(
                paciente,
                "pertenecePuebloIndigena",
                "pertenece_pueblo_indigena"
              )
                ? "Sí"
                : "No"}
            </Campo>

            <Campo label="Migrante">
              {value(paciente, "esMigrante", "es_migrante")
                ? "Sí"
                : "No"}
            </Campo>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <MapPin className="w-5 h-5" />
              Residencia
            </CardTitle>
          </CardHeader>

          <CardContent className="grid sm:grid-cols-2 gap-5">
            <Campo label="Estado">
              {value(
                paciente,
                "estadoResidencia",
                "estado_residencia"
              )}
            </Campo>

            <Campo label="Municipio / Alcaldía">
              {value(
                paciente,
                "municipioResidencia",
                "municipio_residencia"
              )}
            </Campo>

            <Campo label="Localidad">
              {value(paciente, "localidad", "localidad")}
            </Campo>

            <Campo label="Dirección">
              {value(paciente, "direccion", "direccion")}
            </Campo>
          </CardContent>
        </Card>
      </div>

      <Card className="border-amber-200">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ShieldCheck className="w-5 h-5" />
            Información de la baja
          </CardTitle>

          <CardDescription>
            Datos correspondientes a la baja lógica más reciente.
          </CardDescription>
        </CardHeader>

        <CardContent className="grid sm:grid-cols-2 lg:grid-cols-4 gap-5">
          <Campo label="Fecha de baja">
            {fechaHoraMX(
              value(paciente, "fechaBaja", "fecha_baja")
            )}
          </Campo>

          <Campo label="Motivo">
            {value(paciente, "motivoBaja", "motivo_baja")}
          </Campo>

          <Campo label="Usuario">
            {value(paciente, "usuarioBaja", "usuario_baja")}
          </Campo>

          <Campo label="Rol">
            {value(
              paciente,
              "usuarioRolBaja",
              "usuario_rol_baja"
            )}
          </Campo>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileClock className="w-5 h-5" />
            Historial de auditoría
          </CardTitle>

          <CardDescription>
            Eventos administrativos registrados para este paciente.
          </CardDescription>
        </CardHeader>

        <CardContent className="p-0 overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Fecha</TableHead>
                <TableHead>Acción</TableHead>
                <TableHead>Motivo</TableHead>
                <TableHead>Usuario</TableHead>
                <TableHead>Rol</TableHead>
                <TableHead>IP</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {auditoria.length === 0 ? (
                <TableRow>
                  <TableCell
                    colSpan={6}
                    className="text-center py-8 text-muted-foreground"
                  >
                    No existen eventos de auditoría.
                  </TableCell>
                </TableRow>
              ) : (
                auditoria.map((evento, index) => (
                  <TableRow
                    key={
                      value(evento, "id", "id") ??
                      `${value(evento, "accion", "accion")}-${index}`
                    }
                  >
                    <TableCell className="whitespace-nowrap">
                      {fechaHoraMX(
                        value(evento, "fecha", "fecha")
                      )}
                    </TableCell>

                    <TableCell>
                      <Badge
                        variant={
                          value(evento, "accion", "accion") === "BAJA"
                            ? "destructive"
                            : "outline"
                        }
                      >
                        {value(evento, "accion", "accion")}
                      </Badge>
                    </TableCell>

                    <TableCell>
                      {value(evento, "motivo", "motivo") || "—"}
                    </TableCell>

                    <TableCell>
                      {value(
                        evento,
                        "usuarioNombre",
                        "usuario_nombre"
                      ) || "—"}
                    </TableCell>

                    <TableCell>
                      {value(
                        evento,
                        "usuarioRol",
                        "usuario_rol"
                      ) || "—"}
                    </TableCell>

                    <TableCell className="font-mono text-xs">
                      {value(
                        evento,
                        "ipOrigen",
                        "ip_origen"
                      ) || "—"}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}