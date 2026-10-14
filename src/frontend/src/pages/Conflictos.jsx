
import { useEffect, useState } from "react";
import { syncConflictosApi } from "@/api/syncConflictos";

export default function Conflictos() {
  const [conflictos, setConflictos] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState("");
  const [seleccionado, setSeleccionado] = useState(null);
  const [accion, setAccion] = useState("Descartar");
  const [pacienteCentralId, setPacienteCentralId] = useState("");
  const [motivo, setMotivo] = useState("");
  const [guardando, setGuardando] = useState(false);
  const [mensaje, setMensaje] = useState("");

  async function cargarConflictos() {
    setCargando(true);
    setError("");

    try {
      const datos = await syncConflictosApi.pendientes(50);
      setConflictos(Array.isArray(datos) ? datos : []);
    } catch (err) {
      setError(err.message || "No fue posible consultar los conflictos.");
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => {
    cargarConflictos();
  }, []);

  function abrirResolucion(conflicto) {
    setSeleccionado(conflicto);
    setAccion("Descartar");
    setPacienteCentralId("");
    setMotivo("");
    setError("");
    setMensaje("");
  }

  async function resolver(e) {
    e.preventDefault();

    if (!seleccionado || guardando) return;

    const conflictoId = seleccionado.ConflictoId;
    const motivoLimpio = motivo.trim();

    if (motivoLimpio.length < 10 || motivoLimpio.length > 1000) {
      setError("El motivo debe tener entre 10 y 1000 caracteres.");
      return;
    }

    if (
      accion === "VincularExistente" &&
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
        pacienteCentralId.trim()
      )
    ) {
      setError("Introduce un GUID válido del paciente central.");
      return;
    }

    if (!window.confirm(
      `¿Confirmas la acción "${accion}" para el conflicto #${conflictoId}?`
    )) return;

    setGuardando(true);
    setError("");
    setMensaje("");

    try {
      await syncConflictosApi.resolver(
        conflictoId,
        accion,
        accion === "VincularExistente" ? pacienteCentralId.trim() : null,
        motivoLimpio
      );

      setSeleccionado(null);
      setMensaje(`Conflicto #${conflictoId} procesado correctamente.`);
      await cargarConflictos();
    } catch (err) {
      if (err.status === 409) {
        setError(
          `No se pudo resolver el conflicto: ${err.message}. ` +
          "Actualiza la bandeja para verificar su estado."
        );
      } else {
        setError(err.message || "No fue posible resolver el conflicto.");
      }
    } finally {
      setGuardando(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">
            Conflictos de sincronización
          </h1>
          <p className="text-sm text-muted-foreground">
            Revisión de operaciones recibidas desde dispositivos.
          </p>
        </div>

        <button
          type="button"
          onClick={cargarConflictos}
          disabled={cargando || guardando}
          className="rounded-md border px-4 py-2 text-sm"
        >
          {cargando ? "Actualizando..." : "Actualizar"}
        </button>
      </div>

      {mensaje && (
        <div role="status" className="rounded-md border p-3 text-sm">
          {mensaje}
        </div>
      )}

      {error && (
        <div role="alert" className="rounded-md border border-red-500 p-3 text-sm text-red-700">
          {error}
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border">
        <table className="w-full text-left text-sm">
          <thead className="bg-muted">
            <tr>
              <th className="p-3">ID</th>
              <th className="p-3">Entidad</th>
              <th className="p-3">Tipo</th>
              <th className="p-3">Código</th>
              <th className="p-3">CLUES</th>
              <th className="p-3">Acción</th>
            </tr>
          </thead>

          <tbody>
            {conflictos.map((c) => (
              <tr key={c.ConflictoId} className="border-t">
                <td className="p-3">{c.ConflictoId}</td>
                <td className="p-3">{c.Entidad}</td>
                <td className="p-3">{c.Tipo}</td>
                <td className="p-3">{c.Codigo}</td>
                <td className="p-3">{c.Clues}</td>
                <td className="p-3">
                  <button
                    type="button"
                    onClick={() => abrirResolucion(c)}
                    className="rounded-md border px-3 py-1"
                  >
                    Revisar
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {!cargando && !error && conflictos.length === 0 && (
          <p className="p-5 text-center text-muted-foreground">
            No hay conflictos pendientes.
          </p>
        )}
      </div>

      {seleccionado && (
        <form onSubmit={resolver} className="space-y-4 rounded-lg border p-5">
          <h2 className="text-lg font-semibold">
            Resolver conflicto #{seleccionado.ConflictoId}
          </h2>

          <p className="text-sm">
            {seleccionado.Descripcion}
          </p>

          <div>
            <label htmlFor="accion-sync" className="block text-sm font-medium">
              Acción
            </label>
            <select
              id="accion-sync"
              value={accion}
              onChange={(e) => setAccion(e.target.value)}
              className="mt-1 w-full rounded-md border bg-background p-2"
            >
              <option value="Descartar">Descartar</option>
              <option value="VincularExistente">
                Vincular paciente existente
              </option>
            </select>
          </div>

          {accion === "VincularExistente" && (
            <div>
              <label htmlFor="paciente-central" className="block text-sm font-medium">
                GUID del paciente central verificado
              </label>
              <input
                id="paciente-central"
                value={pacienteCentralId}
                onChange={(e) => setPacienteCentralId(e.target.value)}
                className="mt-1 w-full rounded-md border bg-background p-2"
                required
              />
              <p className="mt-1 text-xs text-muted-foreground">
                Verifica previamente la identidad del paciente.
                El GUID por sí solo no demuestra que corresponda
                a la misma persona.
              </p>
            </div>
          )}

          <div>
            <label htmlFor="motivo-sync" className="block text-sm font-medium">
              Motivo de la resolución
            </label>
            <textarea
              id="motivo-sync"
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              maxLength={1000}
              rows={4}
              required
              className="mt-1 w-full rounded-md border bg-background p-2"
            />
            <p className="text-xs text-muted-foreground">
              {motivo.trim().length}/1000 caracteres. Mínimo 10.
            </p>
          </div>

          <div className="flex gap-2">
            <button
              type="submit"
              disabled={guardando}
              className="rounded-md bg-primary px-4 py-2 text-primary-foreground disabled:opacity-50"
            >
              {guardando ? "Procesando..." : "Confirmar resolución"}
            </button>

            <button
              type="button"
              disabled={guardando}
              onClick={() => setSeleccionado(null)}
              className="rounded-md border px-4 py-2"
            >
              Cancelar
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
