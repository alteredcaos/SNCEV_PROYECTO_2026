import React, { useRef, useState } from "react";
import { Search, Loader2, MapPin } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";

// La instancia pública de Nominatim no permite autocomplete por cada pulsación.
// Para producción usar un servicio propio/contratado que admita sugerencias.
const GEO_URL = (import.meta.env.VITE_GEOCODER_URL || "https://nominatim.openstreetmap.org/search").trim();
const CDMX_VIEWBOX = "-99.36,19.59,-98.94,19.19";
let ultimaConsulta = 0;

function consultaNormalizada(q) {
  return q.trim().replace(/\s+/g, " ");
}

function esConsultaCdmx(q) {
  return /(?:ciudad de m[eé]xico|\bcdmx\b|\bdistrito federal\b|\bcuauht[eé]moc\b|\bazcapotzalco\b|\biztapalapa\b|\bcoyoac[aá]n\b)/i.test(q);
}

async function consultar(q, { acotarCdmx = false } = {}) {
  const url = new URL(GEO_URL, window.location.origin);
  url.searchParams.set("q", q);
  url.searchParams.set("format", "jsonv2");
  url.searchParams.set("addressdetails", "1");
  url.searchParams.set("limit", "6");
  url.searchParams.set("countrycodes", "mx");
  if (acotarCdmx) {
    url.searchParams.set("viewbox", CDMX_VIEWBOX);
    url.searchParams.set("bounded", "1");
  }
  const espera = Math.max(0, 1200 - (Date.now() - ultimaConsulta));
  if (espera) await new Promise(resolve => setTimeout(resolve, espera));
  ultimaConsulta = Date.now();
  const respuesta = await fetch(url.toString(), {
    headers: { Accept: "application/json", "Accept-Language": "es" }
  });
  if (!respuesta.ok) throw new Error(`El servicio geográfico respondió ${respuesta.status}.`);
  const datos = await respuesta.json();
  if (!Array.isArray(datos)) throw new Error("Respuesta geográfica inválida.");
  return datos.filter(r => Number.isFinite(Number(r.lat)) && Number.isFinite(Number(r.lon)));
}

export default function GeocodificadorDireccion({ onSelect, disabled = false }) {
  const [texto, setTexto] = useState("");
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState("");
  const [resultados, setResultados] = useState([]);
  const enCurso = useRef(false);

  async function buscar() {
    const q = consultaNormalizada(texto);
    if (q.length < 3) {
      setError("Escribe al menos 3 caracteres de calle, colonia o municipio.");
      return;
    }
    if (enCurso.current || disabled) return;
    enCurso.current = true;
    setCargando(true);
    setError("");
    setResultados([]);
    try {
      // Se busca el texto tal cual. No añadir CDMX: impediría hallar Atizapán,
      // Naucalpan, Tlalnepantla y otros municipios del Estado de México.
      // Si se especifica CDMX, se prioriza su caja geográfica.
      let datos = await consultar(q, { acotarCdmx: esConsultaCdmx(q) });
      if (!datos.length && esConsultaCdmx(q)) {
        datos = await consultar(q);
      }
      setResultados(datos);
      if (!datos.length) {
        setError("No hubo coincidencias. Prueba con una calle más corta y agrega municipio y estado (ej. Ignacio Zaragoza Norte, Atizapán de Zaragoza, Estado de México).");
      }
    } catch (e) {
      setError(e.message || "No fue posible consultar el buscador geográfico.");
    } finally {
      enCurso.current = false;
      setCargando(false);
    }
  }

  return (
    <div className="rounded-md border p-3 space-y-2">
      <div className="text-sm font-medium flex items-center gap-2">
        <Search className="h-4 w-4" /> Buscar calle, colonia o código postal
      </div>
      <div className="flex flex-wrap gap-2">
        <Input
          className="min-w-[200px] flex-1"
          maxLength={180}
          value={texto}
          onChange={e => { setTexto(e.target.value); setError(""); }}
          placeholder="Ej. Ignacio Zaragoza Norte, Atizapán de Zaragoza"
          aria-label="Dirección para buscar"
          disabled={disabled || cargando}
          onKeyDown={e => {
            if (e.key === "Enter") {
              e.preventDefault();
              e.stopPropagation();
              buscar();
            }
          }}
        />
        <Button type="button" variant="secondary" onClick={buscar} disabled={disabled || cargando}>
          {cargando ? <Loader2 className="h-4 w-4 animate-spin mr-1" /> : <Search className="h-4 w-4 mr-1" />}
          Buscar
        </Button>
      </div>
      <p className="text-xs text-muted-foreground">
        Busca por nombre parcial y confirma la coincidencia en el mapa. No introduzcas CURP ni datos nominales.
        La calle, numeración, colonia y código postal deben verificarse antes de guardar.
      </p>
      {error && <p role="alert" className="text-sm text-amber-700">{error}</p>}
      {resultados.length > 0 && (
        <div className="space-y-1 max-h-64 overflow-y-auto" aria-label="Coincidencias geográficas">
          {resultados.map((r, i) => (
            <button
              key={`${r.place_id ?? i}-${r.lat}-${r.lon}`}
              type="button"
              disabled={disabled}
              className="w-full text-left rounded border px-3 py-2 text-sm hover:bg-muted focus-visible:outline focus-visible:outline-2"
              onClick={() => {
                onSelect({
                  latitud: Number(r.lat),
                  longitud: Number(r.lon),
                  direccion: r.address || {},
                  descripcion: r.display_name,
                  boundingbox: r.boundingbox || null
                });
                setTexto(r.display_name?.split(",").slice(0, 2).join(",").trim() || texto);
                setResultados([]);
              }}
            >
              <MapPin className="h-4 w-4 inline mr-2" />{r.display_name}
            </button>
          ))}
        </div>
      )}
      <p className="text-[11px] text-muted-foreground">
        Geocodificación: © OpenStreetMap contributors / Nominatim. El servicio público solo admite búsquedas manuales,
        no autocompletado; para sugerencias al escribir configura un proveedor autorizado.
      </p>
    </div>
  );
}
