import React, { useEffect, useMemo, useState } from "react";
import apiClient, { getStoredToken } from "@/api/apiClient";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, Tooltip, CartesianGrid, LineChart, Line } from "recharts";

const hoy = new Date();
const fechaLocal = (d) => `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
const inicio = `${hoy.getFullYear()}-01-01`;
const num = (n) => Number(n || 0).toLocaleString("es-MX");
const lista = (r) => Array.isArray(r) ? r : r?.filas ?? r?.Filas ?? [];
const valor = (o, k) => o?.[k] ?? o?.[k.charAt(0).toUpperCase()+k.slice(1)];

const EtiquetaUnidad = ({ x, y, payload }) => {
  const raw = String(payload?.value || "");
  const [clave, nombreOriginal] = raw.split("||");
  const nombre = nombreOriginal && nombreOriginal !== clave ? nombreOriginal : "Unidad sin nombre";
  const palabras = nombre.split(/\s+/);
  const lineas = [];
  let actual = "";
  for (const palabra of palabras) {
    if ((actual + " " + palabra).trim().length > 31 && actual) {
      lineas.push(actual); actual = palabra;
    } else actual = (actual + " " + palabra).trim();
  }
  if (actual) lineas.push(actual);
  return <text x={x - 7} y={y - Math.min(lineas.length, 3) * 7} textAnchor="end" fill="currentColor" fontSize="10">
    {lineas.slice(0,3).map((linea,i) => <tspan key={i} x={x-7} dy={i ? 12 : 0}>{linea}</tspan>)}
    <tspan x={x-7} dy="14" fontSize="10" opacity="0.65">{clave}</tspan>
  </text>;
};

export default function Reportes() {
  const [desde, setDesde] = useState(inicio);
  const [hasta, setHasta] = useState(fechaLocal(hoy));
  const [clues, setClues] = useState("");
  const [unidadesCatalogo, setUnidadesCatalogo] = useState([]);
  const [busquedaUnidad, setBusquedaUnidad] = useState("");
  const [selectorAbierto, setSelectorAbierto] = useState(false);
  const [errorCatalogo, setErrorCatalogo] = useState("");
  const [filtros, setFiltros] = useState({ desde: inicio, hasta: fechaLocal(hoy), clues: "" });
  const [resumen, setResumen] = useState(null);
  const [biologicos, setBiologicos] = useState([]);
  const [vacunadores, setVacunadores] = useState([]);
  const [unidades, setUnidades] = useState([]);
  const [agebs, setAgebs] = useState([]);
  const [detalleUnidades, setDetalleUnidades] = useState([]);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState("");
  const [tab, setTab] = useState("resumen");
  const [censo, setCenso] = useState(null);
  const [paginaCenso, setPaginaCenso] = useState(1);
  const [buscarCenso, setBuscarCenso] = useState("");
  const filtrosCensoVacios = { sexo: "", cartilla: "", edadMin: "", edadMax: "", ageb: "" };
  const [borradorCenso, setBorradorCenso] = useState(filtrosCensoVacios);
  const [filtrosCenso, setFiltrosCenso] = useState(filtrosCensoVacios);
  const [cargandoCenso, setCargandoCenso] = useState(false);
  const [errorCenso, setErrorCenso] = useState("");
  const [evaluacion, setEvaluacion] = useState(null);
  const [evaluandoId, setEvaluandoId] = useState(null);
  const [exportandoCenso, setExportandoCenso] = useState("");

  useEffect(() => {
    let activo = true;
    apiClient.get("/reportes/catalogo-unidades")
      .then(r => { if (activo) setUnidadesCatalogo(lista(r)); })
      .catch(e => { if (activo) setErrorCatalogo(e.message || "No se pudo cargar el catálogo de unidades."); });
    return () => { activo = false; };
  }, []);

  const unidadesFiltradas = useMemo(() => {
    const q = busquedaUnidad.trim().toLocaleLowerCase("es-MX");
    return unidadesCatalogo.filter(r => !q ||
      String(valor(r, "clues") || "").toLocaleLowerCase("es-MX").includes(q) ||
      String(valor(r, "nombreUnidad") || "").toLocaleLowerCase("es-MX").includes(q)
    ).slice(0, 40);
  }, [unidadesCatalogo, busquedaUnidad]);

  // Catálogo único para presentar nombres, independientemente de la respuesta de /unidades.
  const nombresPorClues = useMemo(() => new Map(
    unidadesCatalogo.map(r => [String(valor(r, "clues") || "").trim().toUpperCase(),
      String(valor(r, "nombreUnidad") || "").trim()]).filter(([clave]) => clave)
  ), [unidadesCatalogo]);
  const nombreDeUnidad = (r) => {
    const clave = String(valor(r, "clues") || "").trim().toUpperCase();
    return nombresPorClues.get(clave) || valor(r, "nombreUnidad") || "Sin nombre en catálogo";
  };

  const describirAgeb = (r) => {
    const clave = valor(r, "ageb");
    const nombre = valor(r, "nombreAgeb");
    const tipo = valor(r, "tipoAgeb");
    const tipoTexto = tipo === "U" ? "Urbana" : tipo === "R" ? "Rural" : "";
    return { clave, nombre: nombre || (clave ? "AGEB sin descripción en catálogo" : "Sin AGEB"), tipoTexto };
  };

  const seleccionarUnidad = (r) => {
    const clave = String(valor(r, "clues") || "");
    setClues(clave);
    setBusquedaUnidad(`${valor(r, "nombreUnidad") || clave} — ${clave}`);
    setSelectorAbierto(false);
  };

  useEffect(() => {
    let activo = true;
    const cargar = async () => {
      setCargando(true); setError("");
      const p = new URLSearchParams({ desde: filtros.desde, hasta: filtros.hasta });
      if (filtros.clues) p.set("clues", filtros.clues);
      try {
        const [r, b, v, u, a] = await Promise.all([
          apiClient.get(`/reportes/resumen?${p}`),
          apiClient.get(`/reportes/biologicos?${p}`),
          apiClient.get(`/reportes/vacunadores?${p}`),
          apiClient.get(`/reportes/unidades?${p}`),
          apiClient.get(`/reportes/ageb?${p}`),
        ]);
        if (!activo) return;
        setResumen(r); setBiologicos(lista(b)); setVacunadores(lista(v)); setUnidades(lista(u)); setDetalleUnidades(u?.detalle ?? u?.Detalle ?? []); setAgebs(lista(a));
      } catch (e) {
        if (activo) { setResumen(null); setBiologicos([]); setVacunadores([]); setUnidades([]); setDetalleUnidades([]); setAgebs([]); setError(e.message || "No se pudieron cargar los reportes."); }
      } finally { if (activo) setCargando(false); }
    };
    cargar();
    return () => { activo = false; };
  }, [filtros]);

  useEffect(() => {
    if (tab !== "censo") return;
    let activo = true;
    setCargandoCenso(true); setErrorCenso("");
    const p = new URLSearchParams({ fechaCorte: filtros.hasta, pagina: String(paginaCenso), tamano: "20" });
    if (filtros.clues) p.set("clues", filtros.clues);
    if (buscarCenso.trim()) p.set("buscar", buscarCenso.trim());
    Object.entries(filtrosCenso).forEach(([k,v]) => { if (v !== "") p.set(k,v); });
    apiClient.get(`/reportes/censo?${p}`)
      .then(r => { if (activo) setCenso(r); })
      .catch(e => { if (activo) { setCenso(null); setErrorCenso(e.message || "No se pudo consultar el censo."); } })
      .finally(() => { if (activo) setCargandoCenso(false); });
    return () => { activo = false; };
  }, [tab, filtros, paginaCenso, buscarCenso, filtrosCenso]);

  const exportarCenso = async (formato) => {
    if (!censo || cargandoCenso || exportandoCenso) return;
    setExportandoCenso(formato); setErrorCenso("");
    const p = new URLSearchParams({ formato, fechaCorte: filtros.hasta });
    if (filtros.clues) p.set("clues", filtros.clues);
    if (buscarCenso.trim()) p.set("buscar", buscarCenso.trim());
    Object.entries(filtrosCenso).forEach(([k,v]) => { if (v !== "") p.set(k,v); });
    // Descarga binaria autenticada; apiClient.get interpreta el contenido como texto.
    const base = (import.meta.env.VITE_API_URL || "/api/v1").replace(/\/$/, "");
    try {
      const token = getStoredToken();
      if (!token) throw new Error("La sesión ha expirado. Inicia sesión nuevamente.");
      const response = await fetch(`${base}/reportes/censo/exportar?${p}`, {
        headers: { Authorization: `Bearer ${token}` }, cache: "no-store",
      });
      if (!response.ok) {
        const err = await response.json().catch(() => ({}));
        throw new Error(err.message || `Error HTTP ${response.status}`);
      }
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url; a.download = `SNCEV_Censo_${filtros.hasta}.${formato}`;
      document.body.appendChild(a); a.click(); a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 15000);
    } catch (e) { setErrorCenso(e.message || "No fue posible exportar el censo."); }
    finally { setExportandoCenso(""); }
  };

  const consultarEvaluacion = async (id) => {
    setEvaluandoId(id); setEvaluacion(null); setErrorCenso("");
    try {
      const p = new URLSearchParams({ fechaCorte: filtros.hasta });
      const r = await apiClient.get(`/reportes/censo/${id}/evaluacion?${p}`);
      setEvaluacion(r);
    } catch (e) { setErrorCenso(e.message || "No se pudo evaluar el paciente."); }
    finally { setEvaluandoId(null); }
  };

  const totales = resumen?.totales ?? resumen?.Totales ?? {};
  const tendencia = (resumen?.tendencia ?? resumen?.Tendencia ?? []).map(r => ({ mes: valor(r,"mes"), aplicaciones: Number(valor(r,"aplicaciones") || 0), pacientesUnicos: Number(valor(r,"pacientesUnicos") || 0) }));
  const porBiologico = useMemo(() => {
    const m = new Map();
    for (const r of biologicos) {
      const nombre = valor(r,"biologico") || "Sin identificar";
      m.set(nombre, (m.get(nombre) || 0) + Number(valor(r,"aplicaciones") || 0));
    }
    return [...m].map(([nombre, aplicaciones]) => ({ nombre, aplicaciones })).sort((a,b) => b.aplicaciones-a.aplicaciones).slice(0,12);
  }, [biologicos]);
  const kpis = [
    ["Aplicaciones vigentes", valor(totales,"aplicaciones")],
    ["Pacientes vacunados únicos", valor(totales,"pacientesUnicos")],
    ["Biológicos distintos", valor(totales,"biologicos")],
    ["Unidades con aplicaciones", valor(totales,"unidades")],
  ];
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-heading font-semibold">Reportes y análisis vacunal</h1>
        <p className="text-sm text-muted-foreground">Actividad nominal registrada en SNCEV. No equivale a cobertura poblacional oficial.</p>
      </div>
      <Card><CardContent className="p-4 flex flex-wrap gap-3 items-end">
        <label className="text-sm space-y-1">Desde<Input type="date" value={desde} onChange={e=>setDesde(e.target.value)} /></label>
        <label className="text-sm space-y-1">Hasta<Input type="date" value={hasta} onChange={e=>setHasta(e.target.value)} /></label>
        <div className="relative w-full sm:w-[370px] text-sm space-y-1">
          <label htmlFor="unidad-reporte">Unidad médica (nombre o CLUES)</label>
          <div className="flex gap-1">
            <Input id="unidad-reporte" value={busquedaUnidad} autoComplete="off"
              placeholder="Todas las unidades autorizadas"
              onFocus={() => setSelectorAbierto(true)}
              onChange={e => { setBusquedaUnidad(e.target.value); setClues(""); setSelectorAbierto(true); }} />
            <Button type="button" variant="outline" title="Todas las unidades"
              onClick={() => { setClues(""); setBusquedaUnidad(""); setSelectorAbierto(false); }}>Todas</Button>
          </div>
          {selectorAbierto && <div className="absolute z-50 top-full mt-1 w-full max-h-60 overflow-y-auto rounded-md border bg-background shadow-lg">
            {unidadesFiltradas.map(r => <button type="button" key={valor(r,"clues")}
              onClick={() => seleccionarUnidad(r)}
              className="block w-full text-left px-3 py-2 hover:bg-muted border-b last:border-b-0">
              <span className="block text-xs font-medium break-words">{valor(r,"nombreUnidad") || "Sin nombre"}</span>
              <span className="block text-[11px] text-muted-foreground">{valor(r,"clues")}</span>
            </button>)}
            {!unidadesFiltradas.length && <p className="p-3 text-xs text-muted-foreground">Sin coincidencias.</p>}
          </div>}
          {busquedaUnidad && !clues && <p className="text-xs text-amber-700">Selecciona una unidad de la lista o pulsa Todas.</p>}
          {errorCatalogo && <p className="text-xs text-red-600">{errorCatalogo}</p>}
        </div>
        <Button disabled={cargando || !desde || !hasta || desde>hasta || (!!busquedaUnidad && !clues)} onClick={()=>(setPaginaCenso(1), setEvaluacion(null), setFiltros({desde,hasta,clues:clues.trim()}))}>Consultar</Button>
      </CardContent></Card>
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
      {cargando && <p className="text-sm text-muted-foreground">Consultando registros...</p>}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        {kpis.map(([titulo,n])=><Card key={titulo}><CardContent className="p-4"><p className="text-xs text-muted-foreground">{titulo}</p><p className="text-2xl font-semibold">{num(n)}</p></CardContent></Card>)}
      </div>
      <Tabs value={tab} onValueChange={setTab}>
        <TabsList className="flex flex-wrap h-auto">
          {[['resumen','Resumen'],['biologicos','Biológicos'],['vacunadores','Vacunadores'],['ageb','AGEB'],['unidad','Unidad'],['censo','Censo']].map(([v,l])=><TabsTrigger key={v} value={v}>{l}</TabsTrigger>)}
        </TabsList>
        <TabsContent value="resumen"><Card><CardHeader><CardTitle>Aplicaciones por mes</CardTitle><CardDescription>Conteo de dosis no anuladas según fecha de aplicación.</CardDescription></CardHeader><CardContent className="h-80">
          <ResponsiveContainer width="100%" height="100%"><LineChart data={tendencia}><CartesianGrid strokeDasharray="3 3"/><XAxis dataKey="mes"/><YAxis allowDecimals={false}/><Tooltip/><Line type="monotone" dataKey="aplicaciones" name="Aplicaciones" stroke="#2563eb" strokeWidth={2}/></LineChart></ResponsiveContainer>
        </CardContent></Card></TabsContent>
        <TabsContent value="biologicos" className="space-y-4">
          <Card><CardHeader><CardTitle>Aplicaciones por biológico</CardTitle></CardHeader><CardContent className="h-80"><ResponsiveContainer width="100%" height="100%"><BarChart data={porBiologico} layout="vertical" margin={{left:25,right:20}}><CartesianGrid strokeDasharray="3 3"/><XAxis type="number" allowDecimals={false}/><YAxis type="category" dataKey="nombre" width={190} tick={{fontSize:11}}/><Tooltip/><Bar dataKey="aplicaciones" fill="#2563eb"/></BarChart></ResponsiveContainer></CardContent></Card>
          <Card><CardHeader><CardTitle>Detalle de biológico, lote y CLUES</CardTitle></CardHeader><CardContent className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left">{['Biológico','Dosis','Lote','Laboratorio','Caducidad','CLUES','Aplicaciones'].map(x=><th className="p-2" key={x}>{x}</th>)}</tr></thead><tbody>{biologicos.map((r,i)=><tr className="border-b" key={i}><td className="p-2">{valor(r,'biologico')}</td><td className="p-2">{valor(r,'numeroDosis')}</td><td className="p-2">{valor(r,'lote')}</td><td className="p-2">{valor(r,'laboratorio') || 'No registrado'}</td><td className="p-2">{valor(r,'fechaCaducidad')?.slice?.(0,10) || 'Sin catalogar'}</td><td className="p-2"><span className="block text-xs font-medium">{nombreDeUnidad(r)}</span><span className="block text-[11px] text-muted-foreground">{valor(r,'clues')}</span></td><td className="p-2">{num(valor(r,'aplicaciones'))}</td></tr>)}</tbody></table>{!biologicos.length && <p className="p-3 text-muted-foreground">Sin aplicaciones en el periodo.</p>}</CardContent></Card>
        </TabsContent>
        <TabsContent value="vacunadores"><Card><CardHeader><CardTitle>Productividad registrada por vacunador</CardTitle><CardDescription>Dosis por día con al menos una aplicación registrada; no representa eficiencia individual.</CardDescription></CardHeader><CardContent className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left">{['Vacunador','Dosis','Pacientes únicos','Biológicos','Días activos','Dosis/día activo'].map(x=><th className="p-2" key={x}>{x}</th>)}</tr></thead><tbody>{vacunadores.map((r,i)=><tr className="border-b" key={i}><td className="p-2">{valor(r,'vacunador')}</td>{['aplicaciones','pacientesUnicos','biologicos','diasActivos','dosisPorDiaActivo'].map(k=><td className="p-2" key={k}>{num(valor(r,k))}</td>)}</tr>)}</tbody></table>{!vacunadores.length && <p className="p-3 text-muted-foreground">Sin aplicaciones en el periodo.</p>}</CardContent></Card></TabsContent>
        <TabsContent value="unidad" className="space-y-4">
          <Card><CardHeader><CardTitle>Aplicaciones por unidad médica (CLUES)</CardTitle><CardDescription>Actividad registrada en la unidad aplicadora, no cobertura territorial.</CardDescription></CardHeader><CardContent style={{height: Math.max(320, unidades.length * 76 + 90)}}>
            <ResponsiveContainer width="100%" height="100%"><BarChart data={unidades.map(r=>({ etiqueta:`${valor(r,'clues')}||${nombreDeUnidad(r)}`, nombreUnidad:nombreDeUnidad(r), clues:valor(r,'clues'), aplicaciones:Number(valor(r,'aplicaciones')||0) }))} layout="vertical" margin={{left:10,right:20}}><CartesianGrid strokeDasharray="3 3"/><XAxis type="number" allowDecimals={false}/><YAxis type="category" dataKey="etiqueta" width={285} interval={0} tick={<EtiquetaUnidad />}/><Tooltip formatter={v=>[v,"Aplicaciones"]} labelFormatter={(_,items)=>items?.[0]?.payload ? `${items[0].payload.nombreUnidad} (${items[0].payload.clues})` : ""}/><Bar dataKey="aplicaciones" fill="#2563eb"/></BarChart></ResponsiveContainer>
          </CardContent></Card>
          <Card><CardHeader><CardTitle>Indicadores por unidad</CardTitle></CardHeader><CardContent className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left">{['Unidad médica / CLUES','Aplicaciones','Pacientes únicos','Biológicos','Días con actividad'].map(x=><th className="p-2" key={x}>{x}</th>)}</tr></thead><tbody>{unidades.map((r,i)=><tr key={i} className="border-b"><td className="p-2"><span className="block text-xs font-medium">{nombreDeUnidad(r)}</span><span className="block text-[11px] text-muted-foreground">{valor(r,'clues')}</span></td>{['aplicaciones','pacientesUnicos','biologicos','diasConActividad'].map(k=><td key={k} className="p-2">{num(valor(r,k))}</td>)}</tr>)}</tbody></table>{!unidades.length && <p className="p-3 text-muted-foreground">Sin aplicaciones en el periodo.</p>}</CardContent></Card>
          <Card><CardHeader><CardTitle>Detalle de biológicos por CLUES</CardTitle></CardHeader><CardContent className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left">{['Unidad médica / CLUES','Biológico','Aplicaciones','Pacientes únicos'].map(x=><th key={x} className="p-2">{x}</th>)}</tr></thead><tbody>{detalleUnidades.map((r,i)=><tr className="border-b" key={i}><td className="p-2"><span className="block text-xs font-medium">{nombreDeUnidad(r)}</span><span className="block text-[11px] text-muted-foreground">{valor(r,'clues')}</span></td><td className="p-2">{valor(r,'biologico')}</td><td className="p-2">{num(valor(r,'aplicaciones'))}</td><td className="p-2">{num(valor(r,'pacientesUnicos'))}</td></tr>)}</tbody></table></CardContent></Card>
        </TabsContent>
        <TabsContent value="ageb" className="space-y-4">
          <Card><CardHeader><CardTitle>Población nominal y aplicaciones por AGEB</CardTitle><CardDescription>AGEB del domicilio vigente; no equivale a cobertura poblacional oficial.</CardDescription></CardHeader><CardContent style={{height:Math.max(320,agebs.length*70+80)}}>
            <ResponsiveContainer width="100%" height="100%"><BarChart data={agebs.map(r=>({etiqueta:`${valor(r,'nombre') || 'Sin nombre'} — ${valor(r,'clave')}`,pacientesCaptados:Number(valor(r,'pacientesCaptados')||0),aplicaciones:Number(valor(r,'aplicaciones')||0)}))} layout="vertical" margin={{left:15,right:25}}><CartesianGrid strokeDasharray="3 3"/><XAxis type="number" allowDecimals={false}/><YAxis type="category" dataKey="etiqueta" width={220} interval={0} tick={{fontSize:10}}/><Tooltip/><Bar dataKey="pacientesCaptados" name="Pacientes captados" fill="#2563eb"/><Bar dataKey="aplicaciones" name="Aplicaciones" fill="#16a34a"/></BarChart></ResponsiveContainer>
          </CardContent></Card>
          <Card><CardHeader><CardTitle>Detalle por AGEB</CardTitle><CardDescription>El porcentaje expresa pacientes captados con al menos una aplicación vigente en el periodo, no esquemas completos.</CardDescription></CardHeader><CardContent className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left">{['AGEB / Clave','Captados','Vacunados','Aplicaciones','% con aplicación'].map(x=><th key={x} className="p-2">{x}</th>)}</tr></thead><tbody>{agebs.map((r,i)=><tr key={i} className="border-b"><td className="p-2"><span className="block text-xs font-medium">{valor(r,'nombre') || 'Sin nombre'}</span><span className="block text-[11px] text-muted-foreground">{valor(r,'clave')}</span></td><td className="p-2">{num(valor(r,'pacientesCaptados'))}</td><td className="p-2">{num(valor(r,'pacientesVacunados'))}</td><td className="p-2">{num(valor(r,'aplicaciones'))}</td><td className="p-2">{Number(valor(r,'porcentajeConAplicacion')||0).toLocaleString('es-MX',{maximumFractionDigits:2})}%</td></tr>)}</tbody></table>{!agebs.length && <p className="p-3 text-muted-foreground">Sin pacientes con domicilio vigente en el ámbito consultado.</p>}</CardContent></Card>
          {filtros.clues && <p className="text-xs text-amber-700">Al filtrar por CLUES, la cohorte incluye únicamente pacientes con aplicaciones en esa unidad durante el periodo. No debe interpretarse como población total residente del AGEB.</p>}
        </TabsContent>
        <TabsContent value="censo" className="space-y-4">
          <Card><CardHeader><CardTitle>Censo nominal digital</CardTitle><CardDescription>Pacientes con aplicaciones en la CLUES autorizada a la fecha de corte ({filtros.hasta}). No es cobertura oficial ni certificación de esquema.</CardDescription></CardHeader>
            <CardContent className="space-y-3">
              <Input aria-label="Buscar paciente" placeholder="Buscar por nombre, apellido o ID" value={buscarCenso} onChange={e => { setPaginaCenso(1); setEvaluacion(null); setBuscarCenso(e.target.value); }} />
              <form className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3 items-end rounded-md border p-3" onSubmit={e => {
                e.preventDefault();
                if (borradorCenso.edadMin !== "" && borradorCenso.edadMax !== "" && Number(borradorCenso.edadMin) > Number(borradorCenso.edadMax)) return;
                setPaginaCenso(1); setEvaluacion(null); setFiltrosCenso({...borradorCenso});
              }}>
                <label className="text-xs space-y-1">Sexo
                  <select className="w-full h-10 rounded-md border bg-background px-2" value={borradorCenso.sexo} onChange={e=>setBorradorCenso(v=>({...v,sexo:e.target.value}))}>
                    <option value="">Todos</option><option value="M">Masculino</option><option value="F">Femenino</option>
                  </select>
                </label>
                <label className="text-xs space-y-1">Cartilla
                  <select className="w-full h-10 rounded-md border bg-background px-2" value={borradorCenso.cartilla} onChange={e=>setBorradorCenso(v=>({...v,cartilla:e.target.value}))}>
                    <option value="">Todas</option>
                    <option value="Niñas y Niños (0 a 9 años)">Niñas y niños (0–9)</option>
                    <option value="Adolescentes (10 a 19 años)">Adolescentes (10–19)</option>
                    <option value="Mujeres (20 a 59 años)">Mujeres (20–59)</option>
                    <option value="Hombres (20 a 59 años)">Hombres (20–59)</option>
                    <option value="Personas Mayores (60 años y más)">Personas mayores (60+)</option>
                  </select>
                </label>
                <label className="text-xs space-y-1">Edad mínima (años)
                  <Input type="number" min="0" max="125" placeholder="Sin límite" value={borradorCenso.edadMin} onChange={e=>setBorradorCenso(v=>({...v,edadMin:e.target.value}))}/>
                </label>
                <label className="text-xs space-y-1">Edad máxima (años)
                  <Input type="number" min="0" max="125" placeholder="Sin límite" value={borradorCenso.edadMax} onChange={e=>setBorradorCenso(v=>({...v,edadMax:e.target.value}))}/>
                </label>
                <label className="text-xs space-y-1">AGEB
                  <Input maxLength={13} placeholder="Todas" value={borradorCenso.ageb} onChange={e=>setBorradorCenso(v=>({...v,ageb:e.target.value}))}/>
                </label>
                <div className="sm:col-span-2 lg:col-span-5 flex flex-wrap gap-2 items-center">
                  <Button type="submit" size="sm" disabled={cargandoCenso || (borradorCenso.edadMin !== "" && borradorCenso.edadMax !== "" && Number(borradorCenso.edadMin) > Number(borradorCenso.edadMax))}>Aplicar filtros</Button>
                  <Button type="button" size="sm" variant="outline" onClick={()=>{setBorradorCenso({...filtrosCensoVacios});setFiltrosCenso({...filtrosCensoVacios});setPaginaCenso(1);setEvaluacion(null);}}>Limpiar</Button>
                  <span className="text-xs text-muted-foreground">Filtros aplicados en SQL antes de paginar; edades a la fecha de corte.</span>
                </div>
              </form>
              <div className="flex flex-wrap items-center gap-2">
                <Button type="button" size="sm" variant="outline" disabled={!censo || cargandoCenso || !!exportandoCenso} onClick={() => exportarCenso("xlsx")}>{exportandoCenso === "xlsx" ? "Generando Excel..." : "Exportar Excel"}</Button>
                <Button type="button" size="sm" variant="outline" disabled={!censo || cargandoCenso || !!exportandoCenso} onClick={() => exportarCenso("pdf")}>{exportandoCenso === "pdf" ? "Generando PDF..." : "Exportar PDF"}</Button>
                <span className="text-xs text-muted-foreground">Exporta todos los registros filtrados, no sólo la página visible. Límite: 5,000 pacientes. Contiene datos personales.</span>
              </div>
              {errorCenso && <p role="alert" className="text-sm text-red-600">{errorCenso}</p>}
              {cargandoCenso && <p className="text-sm">Consultando censo...</p>}
              <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-left">{["Paciente", "Nacimiento", "Sexo", "Cartilla", "CLUES", "AGEB", "Aplicaciones", "Acción"].map(x => <th key={x} className="p-2">{x}</th>)}</tr></thead>
                <tbody>{lista(censo).map(r => { const id = valor(r,"pacienteId"); return <tr key={id} className="border-b">
                  <td className="p-2">{[valor(r,"nombre"),valor(r,"primerApellido"),valor(r,"segundoApellido")].filter(Boolean).join(" ")}</td>
                  <td className="p-2">{String(valor(r,"fechaNacimiento") || "").slice(0,10)}</td><td className="p-2">{valor(r,"sexo")}</td>
                  <td className="p-2">{valor(r,"cartilla")}</td><td className="p-2">{valor(r,"clues")}</td><td className="p-2">{valor(r,"ageb") || "Sin AGEB"}</td>
                  <td className="p-2">{num(valor(r,"aplicacionesDocumentadas"))}</td>
                  <td className="p-2"><Button size="sm" variant="outline" disabled={!!evaluandoId} onClick={() => consultarEvaluacion(id)}>{evaluandoId === id ? "Evaluando..." : "Evaluar"}</Button></td>
                </tr>; })}</tbody></table></div>
              {!cargandoCenso && censo && !lista(censo).length && <p className="text-sm text-muted-foreground">Sin pacientes para estos filtros.</p>}
              <div className="flex gap-3 items-center justify-end"><span className="text-xs text-muted-foreground">Total: {num(valor(censo || {},"total"))} · Página {paginaCenso}</span>
                <Button variant="outline" disabled={paginaCenso <= 1 || cargandoCenso} onClick={() => setPaginaCenso(x => x-1)}>Anterior</Button>
                <Button variant="outline" disabled={cargandoCenso || paginaCenso*20 >= Number(valor(censo || {},"total") || 0)} onClick={() => setPaginaCenso(x => x+1)}>Siguiente</Button></div>
            </CardContent></Card>
          {evaluacion && <Card><CardHeader><CardTitle>Evaluación nominal V3</CardTitle><CardDescription>Estado: {valor(evaluacion,"estado") || "NO_EVALUABLE"}. No certifica esquema completo.</CardDescription></CardHeader>
            <CardContent className="space-y-2"><p className="text-sm">Dosis documentadas: {num(valor(evaluacion,"dosisAplicadasDocumentadas"))} · Por verificar: {num(valor(evaluacion,"dosisRequierenVerificacion"))}</p>
              <div className="overflow-x-auto rounded-md border">
                <table className="w-full text-sm">
                  <caption className="text-left font-semibold p-3">Detalle por biológico y dosis — resultado descriptivo V3</caption>
                  <thead><tr className="border-b bg-muted/50 text-left">{["Biológico", "Dosis", "Estado", "Clasificación", "Aplicada", "Fecha mínima", "Fecha máxima", "Motivo"].map(t => <th key={t} scope="col" className="p-2 whitespace-nowrap">{t}</th>)}</tr></thead>
                  <tbody>
                    {(valor(evaluacion,"detalle") || []).map((d,i) => <tr key={`${valor(d,"vacunaId")}-${valor(d,"numeroDosis")}-${i}`} className="border-b align-top">
                      <td className="p-2 font-medium">{valor(d,"vacuna") || "Sin identificar"}</td>
                      <td className="p-2">{valor(d,"numeroDosis") ?? "—"}</td>
                      <td className="p-2"><span className="font-semibold">{valor(d,"estado") || "SIN_ESTADO"}</span></td>
                      <td className="p-2">{valor(d,"clasificacionTemporal") || "—"}</td>
                      <td className="p-2 whitespace-nowrap">{String(valor(d,"fechaAplicacion") || "").slice(0,10) || "—"}</td>
                      <td className="p-2 whitespace-nowrap">{String(valor(d,"fechaMinima") || "").slice(0,10) || "—"}</td>
                      <td className="p-2 whitespace-nowrap">{String(valor(d,"fechaMaxima") || "").slice(0,10) || "—"}</td>
                      <td className="p-2 min-w-[240px]">{valor(d,"motivo") || "Sin motivo informado"}</td>
                    </tr>)}
                  </tbody>
                </table>
                {!(valor(evaluacion,"detalle") || []).length && <p className="p-3 text-sm text-muted-foreground">El evaluador no devolvió detalles individuales para este paciente.</p>}
              </div>
              <p className="text-xs text-muted-foreground">Los estados VERIFICAR e HISTORICA_NO_VERIFICABLE no equivalen a dosis faltantes. Las fechas son referencias del motor y no constituyen indicación clínica.</p>
              {(valor(evaluacion,"advertencias") || []).map((a,i) => <p key={i} className="text-xs text-amber-700">{a}</p>)}
            </CardContent></Card>}
        </TabsContent>
      </Tabs>
      <p className="text-xs text-muted-foreground">Metodología: solo aplicaciones no anuladas. Pacientes únicos se cuentan dentro de cada agrupación; no deben sumarse entre grupos. Los lotes sin registro no tienen caducidad verificable. Las exportaciones nominales están restringidas a usuarios autorizados y contienen datos personales.</p>
    </div>
  );
}
