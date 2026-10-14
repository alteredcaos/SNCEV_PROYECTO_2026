// Datos de ejemplo para SNCEV — reflejan el modelo del backend (sncev_api).
// Se pueden sustituir más adelante por llamadas a la API real.

export const estados = [
  { clave_inegi: "15", nombre: "Estado de México", abreviatura: "Edomex" },
  { clave_inegi: "09", nombre: "Ciudad de México", abreviatura: "CDMX" },
  { clave_inegi: "21", nombre: "Puebla", abreviatura: "Pue." },
];

export const municipios = [
  { id: 1, nombre: "Ecatepec de Morelos", estado_clave: "15", jurisdiccion: "Jurisdicción II" },
  { id: 2, nombre: "Nezahualcóyotl", estado_clave: "15", jurisdiccion: "Jurisdicción III" },
  { id: 3, nombre: "Toluca", estado_clave: "15", jurisdiccion: "Jurisdicción I" },
  { id: 4, nombre: "Iztapalapa", estado_clave: "09", jurisdiccion: "Jurisdicción IV" },
  { id: 5, nombre: "Puebla", estado_clave: "21", jurisdiccion: "Jurisdicción I" },
];

export const unidades = [
  { id: "u1", clues: "CSNEZ001", nombre: "U.M. Nezahualcóyotl Centro", municipio_id: 2, jurisdiccion: "Jurisdicción III" },
  { id: "u2", clues: "CSECA001", nombre: "U.M. Ecatepec Sur", municipio_id: 1, jurisdiccion: "Jurisdicción II" },
  { id: "u3", clues: "CSTOL001", nombre: "U.M. Toluca Centro", municipio_id: 3, jurisdiccion: "Jurisdicción I" },
  { id: "u4", clues: "CSIZT001", nombre: "U.M. Iztapalapa Oriente", municipio_id: 4, jurisdiccion: "Jurisdicción IV" },
];

export const tiposCartilla = [
  { id: 1, nombre: "Niños (0-9 años)", edad_min_dias: 0, edad_max_dias: 365 * 9, color_hex: "#2563eb" },
  { id: 2, nombre: "Adolescentes (10-19 años)", edad_min_dias: 365 * 10, edad_max_dias: 365 * 19, color_hex: "#0891b2" },
  { id: 3, nombre: "Adultos (20+ años)", edad_min_dias: 365 * 20, edad_max_dias: null, color_hex: "#16a34a" },
  { id: 4, nombre: "Gestantes", edad_min_dias: 365 * 12, edad_max_dias: null, color_hex: "#db2777" },
];

export const vacunasCatalogo = [
  { id: 1, clave_censia: "BCG", nombre_generico: "BCG", nombre_comercial: "BCG", via_administracion: "ID", tipo_vacuna: "Tuberculosis", enfermedades: ["Tuberculosis"] },
  { id: 2, clave_censia: "HVB", nombre_generico: "Hepatitis B", nombre_comercial: "Hepatitis B", via_administracion: "IM", tipo_vacuna: "Hepatitis B", enfermedades: ["Hepatitis B"] },
  { id: 3, clave_censia: "PEN", nombre_generico: "Pentavalente", nombre_comercial: "Pentavalente Acelular", via_administracion: "IM", tipo_vacuna: "Difteria/Tétanos/Pertussis/Hib/HepB", enfermedades: ["Difteria", "Tétanos", "Pertussis", "Hib", "Hepatitis B"] },
  { id: 4, clave_censia: "ROT", nombre_generico: "Rotavirus", nombre_comercial: "Rotavirus", via_administracion: "VO", tipo_vacuna: "Rotavirus", enfermedades: ["Rotavirus"] },
  { id: 5, clave_censia: "SRP", nombre_generico: "SRP (Triple Viral)", nombre_comercial: "SRP", via_administracion: "SC", tipo_vacuna: "Sarampión/Rubéola/Parotiditis", enfermedades: ["Sarampión", "Rubéola", "Parotiditis"] },
  { id: 6, clave_censia: "DPT", nombre_generico: "DPT", nombre_comercial: "DPT", via_administracion: "IM", tipo_vacuna: "Difteria/Pertussis/Tétanos", enfermedades: ["Difteria", "Pertussis", "Tétanos"] },
  { id: 7, clave_censia: "VPH", nombre_generico: "VPH", nombre_comercial: "VPH Bivalente", via_administracion: "IM", tipo_vacuna: "Virus del Papiloma Humano", enfermedades: ["VPH"] },
  { id: 8, clave_censia: "INF", nombre_generico: "Influenza", nombre_comercial: "Influenza estacional", via_administracion: "IM", tipo_vacuna: "Influenza", enfermedades: ["Influenza"] },
];

export const esquema = [
  { id: 1, tipo_cartilla_id: 1, vacuna_id: 1, numero_dosis: 1, edad_min_dias: 0, edad_max_dias: 365 * 5, edad_optima_dias: 0, es_refuerzo: false },
  { id: 2, tipo_cartilla_id: 1, vacuna_id: 2, numero_dosis: 1, edad_min_dias: 0, edad_max_dias: 7, edad_optima_dias: 0, es_refuerzo: false },
  { id: 3, tipo_cartilla_id: 1, vacuna_id: 3, numero_dosis: 1, edad_min_dias: 60, edad_max_dias: 365 * 5, edad_optima_dias: 60, es_refuerzo: false },
  { id: 4, tipo_cartilla_id: 1, vacuna_id: 3, numero_dosis: 2, edad_min_dias: 120, edad_max_dias: 365 * 5, edad_optima_dias: 120, es_refuerzo: false },
  { id: 5, tipo_cartilla_id: 1, vacuna_id: 3, numero_dosis: 3, edad_min_dias: 180, edad_max_dias: 365 * 5, edad_optima_dias: 180, es_refuerzo: false },
  { id: 6, tipo_cartilla_id: 1, vacuna_id: 4, numero_dosis: 1, edad_min_dias: 60, edad_max_dias: 365 * 5, edad_optima_dias: 60, es_refuerzo: false },
  { id: 7, tipo_cartilla_id: 1, vacuna_id: 4, numero_dosis: 2, edad_min_dias: 120, edad_max_dias: 365 * 5, edad_optima_dias: 120, es_refuerzo: false },
  { id: 8, tipo_cartilla_id: 1, vacuna_id: 5, numero_dosis: 1, edad_min_dias: 365, edad_max_dias: 365 * 6, edad_optima_dias: 365, es_refuerzo: false },
  { id: 9, tipo_cartilla_id: 2, vacuna_id: 5, numero_dosis: 1, edad_min_dias: 365 * 12, edad_max_dias: 365 * 19, edad_optima_dias: 365 * 12, es_refuerzo: false },
  { id: 10, tipo_cartilla_id: 2, vacuna_id: 7, numero_dosis: 1, edad_min_dias: 365 * 9, edad_max_dias: 365 * 12, edad_optima_dias: 365 * 11, es_refuerzo: false },
  { id: 11, tipo_cartilla_id: 3, vacuna_id: 8, numero_dosis: 1, edad_min_dias: 365 * 50, edad_max_dias: null, edad_optima_dias: 365 * 60, es_refuerzo: true },
  { id: 12, tipo_cartilla_id: 4, vacuna_id: 2, numero_dosis: 1, edad_min_dias: 0, edad_max_dias: null, edad_optima_dias: 0, es_refuerzo: false },
];

export const pacientes = [
  { id: "p1", tipo_cartilla_id: 1, nombre: "Mateo", ap_paterno: "Hernández", ap_materno: "Ramírez", curp: "HERM200915MDFRRNA0", fecha_nacimiento: "2020-09-15", sexo: "M", municipio_nacimiento_id: 4, unidad_med_registro_id: "u4", responsable_principal: "Lucía Ramírez García (Madre)", activo: true },
  { id: "p2", tipo_cartilla_id: 1, nombre: "Valeria", ap_paterno: "Martínez", ap_materno: "López", curp: "MALV210607MDFRRLA1", fecha_nacimiento: "2021-06-07", sexo: "F", municipio_nacimiento_id: 2, unidad_med_registro_id: "u1", responsable_principal: "Carlos López Soto (Padre)", activo: true },
  { id: "p3", tipo_cartilla_id: 2, nombre: "Diego", ap_paterno: "González", ap_materno: "Pérez", curp: "GOPD080512HDFRRNA2", fecha_nacimiento: "2008-05-12", sexo: "M", municipio_nacimiento_id: 1, unidad_med_registro_id: "u2", responsable_principal: "—", activo: true },
  { id: "p4", tipo_cartilla_id: 3, nombre: "Rosa", ap_paterno: "Flores", ap_materno: "Cruz", curp: "FUCR600215MDFRRNA3", fecha_nacimiento: "1960-02-15", sexo: "F", municipio_nacimiento_id: 3, unidad_med_registro_id: "u3", responsable_principal: "—", activo: true },
  { id: "p5", tipo_cartilla_id: 4, nombre: "Ana", ap_paterno: "Ruiz", ap_materno: "Mora", curp: "RUMA900210MDFRRNA4", fecha_nacimiento: "1990-02-10", sexo: "F", municipio_nacimiento_id: 4, unidad_med_registro_id: "u4", responsable_principal: "—", activo: true, gestante_semanas: 24 },
  { id: "p6", tipo_cartilla_id: 1, nombre: "Santiago", ap_paterno: "Torres", ap_materno: "Vega", curp: "TOVS190820HDFRRNA5", fecha_nacimiento: "2019-08-20", sexo: "M", municipio_nacimiento_id: 2, unidad_med_registro_id: "u1", responsable_principal: "María Vega Díaz (Madre)", activo: true },
];

export const vacunasAplicadas = [
  { id: "a1", paciente_id: "p1", vacuna_id: 1, numero_dosis: 1, fecha_aplicacion: "2020-09-16", lote: "BCG-2020-11", laboratorio: "SSA", unidad_medica_id: "u4", nombre_aplicador: "Dra. Karina Mendoza", es_valida: true },
  { id: "a2", paciente_id: "p1", vacuna_id: 2, numero_dosis: 1, fecha_aplicacion: "2020-09-16", lote: "HVB-2020-08", laboratorio: "SSA", unidad_medica_id: "u4", nombre_aplicador: "Dra. Karina Mendoza", es_valida: true },
  { id: "a3", paciente_id: "p1", vacuna_id: 3, numero_dosis: 1, fecha_aplicacion: "2020-11-20", lote: "PENT-2020-05", laboratorio: "BIRMEX", unidad_medica_id: "u4", nombre_aplicador: "Dr. Omar Salazar", es_valida: true },
  { id: "a4", paciente_id: "p1", vacuna_id: 3, numero_dosis: 2, fecha_aplicacion: "2021-01-18", lote: "PENT-2020-05", laboratorio: "BIRMEX", unidad_medica_id: "u4", nombre_aplicador: "Dr. Omar Salazar", es_valida: true },
  { id: "a5", paciente_id: "p2", vacuna_id: 1, numero_dosis: 1, fecha_aplicacion: "2021-06-08", lote: "BCG-2021-03", laboratorio: "SSA", unidad_medica_id: "u1", nombre_aplicador: "Dr. Luis Bravo", es_valida: true },
  { id: "a6", paciente_id: "p2", vacuna_id: 2, numero_dosis: 1, fecha_aplicacion: "2021-06-08", lote: "HVB-2021-02", laboratorio: "SSA", unidad_medica_id: "u1", nombre_aplicador: "Dr. Luis Bravo", es_valida: true },
  { id: "a7", paciente_id: "p3", vacuna_id: 5, numero_dosis: 1, fecha_aplicacion: "2020-05-10", lote: "SRP-2020-01", laboratorio: "BIRMEX", unidad_medica_id: "u2", nombre_aplicador: "Dra. Sofía Núñez", es_valida: true },
  { id: "a8", paciente_id: "p4", vacuna_id: 8, numero_dosis: 1, fecha_aplicacion: "2023-10-15", lote: "INF-2023-09", laboratorio: "BIRMEX", unidad_medica_id: "u3", nombre_aplicador: "Dr. Pedro Lara", es_valida: true },
  { id: "a9", paciente_id: "p5", vacuna_id: 2, numero_dosis: 1, fecha_aplicacion: "2025-09-10", lote: "HVB-2025-01", laboratorio: "SSA", unidad_medica_id: "u4", nombre_aplicador: "Dra. Karina Mendoza", es_valida: true },
  { id: "a10", paciente_id: "p6", vacuna_id: 1, numero_dosis: 1, fecha_aplicacion: "2019-08-21", lote: "BCG-2019-12", laboratorio: "SSA", unidad_medica_id: "u1", nombre_aplicador: "Dr. Luis Bravo", es_valida: true },
  { id: "a11", paciente_id: "p6", vacuna_id: 3, numero_dosis: 1, fecha_aplicacion: "2019-10-25", lote: "PENT-2019-09", laboratorio: "BIRMEX", unidad_medica_id: "u1", nombre_aplicador: "Dr. Luis Bravo", es_valida: true },
  { id: "a12", paciente_id: "p6", vacuna_id: 3, numero_dosis: 2, fecha_aplicacion: "2019-12-30", lote: "PENT-2019-09", laboratorio: "BIRMEX", unidad_medica_id: "u1", nombre_aplicador: "Dr. Luis Bravo", es_valida: true },
  { id: "a13", paciente_id: "p6", vacuna_id: 5, numero_dosis: 1, fecha_aplicacion: "2020-09-05", lote: "SRP-2020-06", laboratorio: "BIRMEX", unidad_medica_id: "u1", nombre_aplicador: "Dra. Ana Ríos", es_valida: true },
];

export const citas = [
  { id: "c1", paciente_id: "p1", fecha: "2026-09-24", hora: "09:30", unidad_medica_id: "u4", vacuna_id: 3, numero_dosis: 3, estado: "programada", notas: "Refuerzo pentavalente" },
  { id: "c2", paciente_id: "p2", fecha: "2026-09-24", hora: "10:15", unidad_medica_id: "u1", vacuna_id: 3, numero_dosis: 1, estado: "programada", notas: "Primera dosis pentavalente" },
  { id: "c3", paciente_id: "p6", fecha: "2026-09-24", hora: "11:00", unidad_medica_id: "u1", vacuna_id: 3, numero_dosis: 3, estado: "programada", notas: "" },
  { id: "c4", paciente_id: "p3", fecha: "2026-09-25", hora: "08:45", unidad_medica_id: "u2", vacuna_id: 7, numero_dosis: 1, estado: "programada", notas: "VPH primera dosis" },
  { id: "c5", paciente_id: "p5", fecha: "2026-09-25", hora: "12:30", unidad_medica_id: "u4", vacuna_id: 2, numero_dosis: 2, estado: "programada", notas: "Gestante - 2da dosis" },
  { id: "c6", paciente_id: "p4", fecha: "2026-09-26", hora: "10:00", unidad_medica_id: "u3", vacuna_id: 8, numero_dosis: 1, estado: "programada", notas: "Influenza estacional" },
];

export const usuarios = [
  { id: "us1", username: "admin", nombre: "Administrador", email: "admin@salud.gob.mx", rol: "Administrador", unidad: "—", activo: true, ultimo_acceso: "2026-09-23 18:42" },
  { id: "us2", username: "k.mendoza", nombre: "Karina Mendoza Ruiz", email: "kmendoza@salud.gob.mx", rol: "Capturador", unidad: "U.M. Iztapalapa Oriente", activo: true, ultimo_acceso: "2026-09-24 08:10" },
  { id: "us3", username: "l.bravo", nombre: "Luis Bravo Soto", email: "lbravo@salud.gob.mx", rol: "Capturador", unidad: "U.M. Nezahualcóyotl Centro", activo: true, ultimo_acceso: "2026-09-24 07:55" },
  { id: "us4", username: "j.cordova", nombre: "Jorge Córdova Lima", email: "jcordova@salud.gob.mx", rol: "Directivo", unidad: "Jurisdicción III", activo: true, ultimo_acceso: "2026-09-23 20:01" },
  { id: "us5", username: "p.lara", nombre: "Pedro Lara Núñez", email: "plara@salud.gob.mx", rol: "Capturador", unidad: "U.M. Toluca Centro", activo: false, ultimo_acceso: "2026-08-30 14:22" },
];

export const roles = [
  { nombre: "Administrador", permisos: ["*"], descripcion: "Acceso total al sistema" },
  { nombre: "Directivo", permisos: ["reportes", "pacientes", "citas", "admin"], descripcion: "Supervisión de cobertura y usuarios en su jurisdicción" },
  { nombre: "Capturador", permisos: ["pacientes", "vacunacion", "citas", "cartilla"], descripcion: "Registro de pacientes y aplicación de dosis" },
];

export const coberturaMunicipios = [
  { municipio: "Nezahualcóyotl", jurisdiccion: "Jurisdicción III", meta: 100, aplicadas: 87, rezago: 13, cobertura: 87 },
  { municipio: "Ecatepec de Morelos", jurisdiccion: "Jurisdicción II", meta: 100, aplicadas: 79, rezago: 21, cobertura: 79 },
  { municipio: "Toluca", jurisdiccion: "Jurisdicción I", meta: 100, aplicadas: 92, rezago: 8, cobertura: 92 },
  { municipio: "Iztapalapa", jurisdiccion: "Jurisdicción IV", meta: 100, aplicadas: 71, rezago: 29, cobertura: 71 },
  { municipio: "Puebla", jurisdiccion: "Jurisdicción I", meta: 100, aplicadas: 84, rezago: 16, cobertura: 84 },
];

export const tendenciaMensual = [
  { mes: "Abr", cobertura: 68 },
  { mes: "May", cobertura: 72 },
  { mes: "Jun", cobertura: 75 },
  { mes: "Jul", cobertura: 78 },
  { mes: "Ago", cobertura: 81 },
  { mes: "Sep", cobertura: 83 },
];

// ── Helpers ──
export const edadEnAnios = (fecha) => {
  const n = new Date(fecha);
  const diff = Date.now() - n.getTime();
  return Math.floor(diff / (365.25 * 24 * 3600 * 1000));
};

export const edadMeses = (fecha) => {
  const n = new Date(fecha);
  return Math.floor((Date.now() - n.getTime()) / (30.44 * 24 * 3600 * 1000));
};

export const nombreCompleto = (p) =>
  [p.nombre, p.ap_paterno, p.ap_materno].filter(Boolean).join(" ");

export const nombreVacuna = (id) => vacunasCatalogo.find((v) => v.id === id)?.nombre_generico ?? "—";
export const nombreTipoCartilla = (id) => tiposCartilla.find((t) => t.id === id)?.nombre ?? "—";
export const nombreUnidad = (id) => unidades.find((u) => u.id === id)?.nombre ?? "—";
export const nombreMunicipio = (id) => municipios.find((m) => m.id === id)?.nombre ?? "—";

export const esquemaParaPaciente = (paciente) => {
  const aplicadas = vacunasAplicadas.filter((a) => a.paciente_id === paciente.id && a.es_valida);
  return esquema
    .filter((e) => e.tipo_cartilla_id === paciente.tipo_cartilla_id)
    .map((e) => {
      const aplicada = aplicadas.find((a) => a.vacuna_id === e.vacuna_id && a.numero_dosis === e.numero_dosis);
      return {
        ...e,
        vacuna: nombreVacuna(e.vacuna_id),
        estado: aplicada ? "aplicada" : "pendiente",
        fecha_aplicacion: aplicada?.fecha_aplicacion ?? null,
        lote: aplicada?.lote ?? null,
        aplicador: aplicada?.nombre_aplicador ?? null,
      };
    });
};