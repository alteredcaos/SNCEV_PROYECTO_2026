import React from "react";
import { Link } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { Syringe, CalendarDays, AlertTriangle, TrendingUp, Users, ArrowRight } from "lucide-react";
import {
  pacientes, citas, vacunasAplicadas, coberturaMunicipios, tendenciaMensual,
  nombreCompleto, nombreVacuna, nombreUnidad, edadEnAnios,
} from "@/lib/mockData";
import { useRole } from "@/lib/RoleContext";
import {
  ResponsiveContainer, AreaChart, Area, XAxis, YAxis, Tooltip, CartesianGrid,
} from "recharts";

function Kpi({ icon: Icon, label, value, sub, tone }) {
  const tones = {
    blue: "bg-blue-50 text-blue-700",
    green: "bg-emerald-50 text-emerald-700",
    amber: "bg-amber-50 text-amber-700",
    violet: "bg-violet-50 text-violet-700",
  };
  return (
    <Card>
      <CardContent className="p-5">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-sm text-muted-foreground">{label}</p>
            <p className="text-3xl font-heading font-semibold mt-1">{value}</p>
            {sub && <p className="text-xs text-muted-foreground mt-1">{sub}</p>}
          </div>
          <div className={`w-11 h-11 rounded-xl flex items-center justify-center ${tones[tone]}`}>
            <Icon className="w-5 h-5" />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export default function Dashboard() {
  const { role } = useRole();
  const hoy = "2026-09-24";
  const citasHoy = citas.filter((c) => c.fecha === hoy);
  const coberturaProm = Math.round(coberturaMunicipios.reduce((s, m) => s + m.cobertura, 0) / coberturaMunicipios.length);
  const dosisTotal = vacunasAplicadas.filter((a) => a.es_valida).length;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-heading font-semibold">Bienvenido</h1>
        <p className="text-muted-foreground text-sm mt-1">
          {role === "vacunador"
            ? "U.M. Iztapalapa Oriente — jornada de vacunación de hoy"
            : "Resumen jurisdiccional de cobertura y rezago"}
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Kpi icon={Users} label="Pacientes activos" value={pacientes.length} sub="En seguimiento" tone="blue" />
        <Kpi icon={CalendarDays} label="Citas de hoy" value={citasHoy.length} sub={`Programadas para ${hoy}`} tone="violet" />
        <Kpi icon={Syringe} label="Dosis aplicadas" value={dosisTotal} sub="Histórico válido" tone="green" />
        <Kpi icon={TrendingUp} label="Cobertura promedio" value={`${coberturaProm}%`} sub="Meta jurisdiccional 90%" tone="amber" />
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Tendencia de cobertura</CardTitle>
            <CardDescription>Últimos 6 meses · porcentaje de meta alcanzada</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="h-64">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={tendenciaMensual} margin={{ left: -20, right: 10, top: 5 }}>
                  <defs>
                    <linearGradient id="cov" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="#2563eb" stopOpacity={0.35} />
                      <stop offset="95%" stopColor="#2563eb" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
                  <XAxis dataKey="mes" tick={{ fontSize: 12 }} stroke="#94a3b8" />
                  <YAxis domain={[0, 100]} tick={{ fontSize: 12 }} stroke="#94a3b8" />
                  <Tooltip contentStyle={{ borderRadius: 8, fontSize: 12 }} formatter={(v) => [`${v}%`, "Cobertura"]} />
                  <Area type="monotone" dataKey="cobertura" stroke="#2563eb" strokeWidth={2} fill="url(#cov)" />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Cobertura por municipio</CardTitle>
            <CardDescription>Rezago vacunal detectado</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {coberturaMunicipios.slice(0, 5).map((m) => (
              <div key={m.municipio}>
                <div className="flex justify-between text-sm mb-1">
                  <span className="font-medium truncate">{m.municipio}</span>
                  <span className="text-muted-foreground">{m.cobertura}%</span>
                </div>
                <Progress value={m.cobertura} className="h-2" />
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Próximas citas</CardTitle>
            <CardDescription>{citasHoy.length} citas programadas para hoy</CardDescription>
          </div>
          <Link to="/citas">
            <Button variant="outline" size="sm">
              Ver agenda <ArrowRight className="w-4 h-4 ml-1" />
            </Button>
          </Link>
        </CardHeader>
        <CardContent>
          <div className="divide-y">
            {citasHoy.map((c) => {
              const p = pacientes.find((x) => x.id === c.paciente_id);
              return (
                <div key={c.id} className="flex items-center gap-4 py-3">
                  <div className="text-center w-14">
                    <p className="text-xs text-muted-foreground">Hora</p>
                    <p className="font-semibold">{c.hora}</p>
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="font-medium truncate">{nombreCompleto(p)}</p>
                    <p className="text-xs text-muted-foreground truncate">
                      {nombreVacuna(c.vacuna_id)} · dosis {c.numero_dosis} · {nombreUnidad(c.unidad_medica_id)}
                    </p>
                  </div>
                  <span className="text-xs text-muted-foreground hidden sm:block">
                    {edadEnAnios(p.fecha_nacimiento)} años
                  </span>
                </div>
              );
            })}
            {citasHoy.length === 0 && (
              <p className="text-sm text-muted-foreground py-6 text-center">No hay citas para hoy</p>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}