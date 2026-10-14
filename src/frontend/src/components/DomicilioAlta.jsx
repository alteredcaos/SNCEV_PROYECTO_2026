import React, { useEffect, useState } from "react";
import { MapContainer, TileLayer, Marker, useMap, useMapEvents } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import GeocodificadorDireccion from "@/components/GeocodificadorDireccion";
import { apiClient } from "@/api/apiClient";

export const DOMICILIO_INICIAL = {
  localidadClave: "", localidad: "", colonia: "", codigoPostal: "", calle: "",
  numeroExterior: "", numeroInterior: "", manzana: "", lote: "", direccionTexto: "",
  latitud: "", longitud: "", origenGeorreferencia: "MAPA", precisionGeorreferencia: "MANUAL",
};

export function domicilioValido(d) {
  return d && d.latitud !== "" && d.longitud !== "" &&
    Number.isFinite(Number(d.latitud)) && Number.isFinite(Number(d.longitud)) &&
    Number(d.latitud) >= -90 && Number(d.latitud) <= 90 &&
    Number(d.longitud) >= -180 && Number(d.longitud) <= 180;
}

export function payloadDomicilio(d) {
  const campos = ["localidadClave", "localidad", "colonia", "codigoPostal", "calle", "numeroExterior", "numeroInterior", "manzana", "lote", "direccionTexto"];
  const payload = Object.fromEntries(campos.map(k => [k, String(d[k] || "").trim() || null]));
  return { ...payload, latitud: Number(d.latitud), longitud: Number(d.longitud),
    origenGeorreferencia: "MAPA", precisionGeorreferencia: "MANUAL" };
}

function MoverMapa({ latitud, longitud }) {
  const mapa = useMap();
  useEffect(() => { if (Number.isFinite(latitud) && Number.isFinite(longitud)) mapa.setView([latitud, longitud], 17); }, [mapa, latitud, longitud]);
  return null;
}
function ElegirPunto({ onChange, latitud, longitud }) {
  useMapEvents({ click(e) { onChange(e.latlng.lat, e.latlng.lng); } });
  if (!Number.isFinite(latitud) || !Number.isFinite(longitud)) return null;
  return <Marker position={[latitud, longitud]} draggable eventHandlers={{ dragend(e) { const p = e.target.getLatLng(); onChange(p.lat, p.lng); } }}
    icon={L.divIcon({ className: "", html: '<div style="background:#1664a5;border:3px solid white;border-radius:50%;height:22px;width:22px;box-shadow:0 1px 5px #333"></div>', iconSize:[22,22], iconAnchor:[11,11] })}/>;
}

export default function DomicilioAlta({ domicilio, onChange, disabled }) {
  const [geo, setGeo] = useState(null);
  const [geoError, setGeoError] = useState("");
  const [consultando, setConsultando] = useState(false);
  const lat = domicilioValido(domicilio) ? Number(domicilio.latitud) : NaN;
  const lng = domicilioValido(domicilio) ? Number(domicilio.longitud) : NaN;

  useEffect(() => {
    if (!domicilioValido(domicilio)) { setGeo(null); return; }
    let activo = true;
    setConsultando(true); setGeo(null); setGeoError("");
    apiClient.get(`/geografia/resolver?latitud=${encodeURIComponent(lat)}&longitud=${encodeURIComponent(lng)}&limiteClues=5`)
      .then(r => { if (activo) setGeo(r); })
      .catch(err => { if (activo) setGeoError(err.message || "No fue posible resolver la ubicación."); })
      .finally(() => { if (activo) setConsultando(false); });
    return () => { activo = false; };
  }, [lat, lng]);

  function cambiar(latitud, longitud) {
    onChange(f => ({ ...f, latitud: Number(latitud.toFixed(7)), longitud: Number(longitud.toFixed(7)) }));
  }
  function campo(k, etiqueta, maxLength = 150) {
    return <div className="space-y-1"><Label>{etiqueta}</Label><Input maxLength={maxLength} disabled={disabled}
      value={domicilio[k]} onChange={e => onChange(f => ({ ...f, [k]: e.target.value }))}/></div>;
  }
  const v = (obj, ...keys) => keys.map(k => obj?.[k]).find(x => x != null && x !== "");
  const ageb = v(geo, "ageb", "agebResuelta", "ageb_resuelta");
  return <div className="space-y-4 rounded-lg border p-4">
    <div className="font-medium">Domicilio georreferenciado (opcional)</div>
    <p className="text-sm text-muted-foreground">Busca una ubicación o captura los datos manualmente. Si eliges un punto, se guardará un domicilio estructurado después del alta nominal.</p>
    <GeocodificadorDireccion disabled={disabled} onSelect={({latitud,longitud,direccion}) => {
      onChange(f => ({ ...f,
        calle: f.calle || direccion.road || direccion.pedestrian || "",
        colonia: f.colonia || direccion.suburb || direccion.neighbourhood || "",
        codigoPostal: f.codigoPostal || direccion.postcode || "",
        localidad: f.localidad || direccion.city || direccion.town || direccion.village || "",
        latitud: Number(latitud.toFixed(7)), longitud: Number(longitud.toFixed(7)),
      }));
    }}/>
    <div className="h-[300px] overflow-hidden rounded-md border">
      <MapContainer center={[19.432608,-99.133209]} zoom={11} className="h-full w-full" scrollWheelZoom>
        <TileLayer attribution='&copy; OpenStreetMap contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"/>
        <MoverMapa latitud={lat} longitud={lng}/>
        <ElegirPunto latitud={lat} longitud={lng} onChange={cambiar}/>
      </MapContainer>
    </div>
    <p className="text-xs text-muted-foreground">Haz clic en el mapa o arrastra el marcador para precisar la ubicación.</p>
    <div className="grid gap-3 md:grid-cols-2">
      {campo("localidad", "Localidad")}{campo("localidadClave", "Clave de localidad", 10)}
      {campo("colonia", "Colonia")}{campo("codigoPostal", "Código postal", 10)}
      {campo("calle", "Calle", 200)}{campo("numeroExterior", "Número exterior", 30)}
      {campo("numeroInterior", "Número interior", 30)}{campo("manzana", "Manzana", 30)}
      {campo("lote", "Lote", 30)}{campo("direccionTexto", "Dirección / referencia", 500)}
    </div>
    <div className="text-xs space-y-1 rounded-md bg-muted/40 p-3">
      <div>Coordenadas: {domicilioValido(domicilio) ? `${lat.toFixed(6)}, ${lng.toFixed(6)}` : "Sin seleccionar"}</div>
      {consultando && <div>Resolviendo ubicación territorial…</div>}
      {geoError && <div className="text-amber-700">{geoError}</div>}
      {geo && <><div>Municipio: {v(geo, "municipioNombre", "municipio_nombre") || v(geo?.municipio, "nombre", "Nombre") || "No identificado"}</div>
        <div>AGEB: {v(geo, "cveAgeb", "cve_ageb") || v(ageb, "cveAgeb", "cve_ageb") || "No identificada"}</div>
        <div>CVEGEO: {v(geo, "cvegeo") || v(ageb, "cvegeo") || "No identificado"}</div></>}
    </div>
    <p className="text-xs text-muted-foreground">Confirma el estado y municipio de residencia en el formulario nominal; la geocodificación no los cambia automáticamente.</p>
  </div>;
}
