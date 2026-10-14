using Dapper;
using Microsoft.Data.SqlClient;
using SNCEV.Api.Models;

namespace SNCEV.Api.Services;

/// <summary>
/// V3: evaluación descriptiva con metadatos temporales opcionales; no certifica cobertura. V2: evaluación descriptiva por edad y etapa de vida. No determina indicación
/// clínica de rescate, equivalencias de biológicos ni cobertura oficial.
/// </summary>
public sealed class EvaluadorCoberturaNominalService
{
    private readonly IConfiguration _config;
    public EvaluadorCoberturaNominalService(IConfiguration config) => _config = config;

    private static readonly HashSet<string> GruposGenerales = new(StringComparer.OrdinalIgnoreCase)
    {
        "POBLACION_GENERAL", "INFANTIL", "NINOS", "RECIEN_NACIDO",
        "SR_INFANTIL", "SRP_INFANTIL", "INFANTIL_Y_ADULTO_MAYOR"
    };

    private static string Etapa(int edadAnios) => edadAnios switch
    {
        < 10 => "NINAS_NINOS_0_9",
        < 20 => "ADOLESCENTES_10_19",
        < 60 => "ADULTOS_20_59",
        _ => "PERSONAS_MAYORES_60_MAS"
    };

    public async Task<EvaluacionCoberturaNominalDto?> EvaluarAsync(
        Guid pacienteId, DateTime? fechaCorte = null, CancellationToken ct = default)
    {
        var corte = (fechaCorte ?? DateTime.Today).Date;
        if (corte > DateTime.Today || corte.Year < 1900)
            throw new ArgumentOutOfRangeException(nameof(fechaCorte), "Fecha de corte fuera del rango permitido.");

        var cs = _config.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException("Falta SqlServerConnection.");
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);

        var paciente = await conn.QuerySingleOrDefaultAsync<Paciente>(new CommandDefinition(@"
            SELECT id, fecha_nacimiento AS FechaNacimiento, sexo
            FROM sncev.paciente WHERE id = @pacienteId AND eliminado = 0;",
            new { pacienteId }, cancellationToken: ct));
        if (paciente is null) return null;

        var dto = new EvaluacionCoberturaNominalDto { PacienteId = pacienteId, FechaCorte = corte };
        var sexo = paciente.Sexo?.Trim().ToUpperInvariant();
        if (paciente.FechaNacimiento.Date > corte || (sexo != "M" && sexo != "F"))
        {
            dto.Advertencias.Add("Fecha de nacimiento o sexo incompatible con la evaluación.");
            return dto;
        }

        var nacimiento = paciente.FechaNacimiento.Date;
        var edadDias = (corte - nacimiento).Days;
        var edadAnios = corte.Year - nacimiento.Year;
        if (nacimiento.AddYears(edadAnios) > corte) edadAnios--;
        dto.EdadAnios = edadAnios;
        dto.EtapaVida = Etapa(edadAnios);

        var reglas = (await conn.QueryAsync<Regla>(new CommandDefinition(@"
            SELECT r.id AS ReglaId, vc.id AS VacunaId, vc.nombre_generico AS Vacuna,
                   r.numero_dosis AS NumeroDosis, r.tipo_dosis AS TipoDosis,
                   r.grupo_poblacional AS GrupoPoblacional, r.sexo AS Sexo,
                   r.edad_min_dias AS EdadMinDias, r.edad_max_dias AS EdadMaxDias,
                   r.requiere_verificacion AS RequiereVerificacion,
                   vc.intervalo_dias_min AS IntervaloDiasMin,
                   vr.vigente_desde AS VigenteDesde, vr.vigente_hasta AS VigenteHasta,
                   vr.cohorte_nacimiento_desde AS CohorteDesde,
                   vr.cohorte_nacimiento_hasta AS CohorteHasta,
                   vr.validada AS ReglaValidada,
                   eq.grupo_codigo AS GrupoEquivalencia
            FROM sncev.regla_asignacion_biologico r
            INNER JOIN sncev.vacuna_catalogo vc ON vc.clave_anx = r.vacuna_clave_anx
            LEFT JOIN sncev.regla_vigencia_nominal vr ON vr.regla_id = r.id
            LEFT JOIN sncev.equivalencia_biologico_nominal eq
                ON eq.vacuna_id = vc.id AND eq.validada = 1
            WHERE vc.activo = 1 AND r.numero_dosis IS NOT NULL;",
            cancellationToken: ct))).ToList();

        var aplicaciones = (await conn.QueryAsync<Aplicacion>(new CommandDefinition(@"
            SELECT vacuna_id AS VacunaId, numero_dosis AS NumeroDosis,
                   fecha_aplicacion AS FechaAplicacion
            FROM sncev.vacuna_aplicada
            WHERE paciente_id = @pacienteId AND anulada = 0
              AND fecha_aplicacion <= @corte;",
            new { pacienteId, corte }, cancellationToken: ct))).ToList();

        // Una regla histórica sólo se puede atribuir a la cohorte cuando existe
        // metadato explícito validado; nunca extrapolamos el catálogo actual.
        bool CohorteCompatible(Regla r) =>
            (!r.CohorteDesde.HasValue || nacimiento >= r.CohorteDesde.Value.Date) &&
            (!r.CohorteHasta.HasValue || nacimiento <= r.CohorteHasta.Value.Date);
        bool VigenteAlCorte(Regla r) =>
            (!r.VigenteDesde.HasValue || corte >= r.VigenteDesde.Value.Date) &&
            (!r.VigenteHasta.HasValue || corte <= r.VigenteHasta.Value.Date);

        if (reglas.Count == 0)
        {
            dto.Advertencias.Add("No existen reglas activas para evaluar.");
            return dto;
        }

        foreach (var grupo in reglas.GroupBy(r => (r.VacunaId, r.NumeroDosis)))
        {
            var compatibles = grupo.Where(r => (string.IsNullOrWhiteSpace(r.Sexo) ||
                string.Equals(r.Sexo.Trim(), sexo, StringComparison.OrdinalIgnoreCase)) &&
                CohorteCompatible(r) && VigenteAlCorte(r)).ToList();
            if (compatibles.Count == 0)
            {
                // Los antecedentes registrados se preservan aun si ninguna regla
                // vigente corresponde a la cohorte o fecha de corte.
                var antecedente = aplicaciones.FirstOrDefault(a => a.VacunaId == grupo.Key.VacunaId &&
                    a.NumeroDosis == grupo.Key.NumeroDosis);
                if (antecedente is not null)
                {
                    var d = Nuevo(grupo.First(), "APLICADA", "Antecedente documentado; regla no vigente o no compatible con cohorte.");
                    d.FechaAplicacion = antecedente.FechaAplicacion;
                    d.ClasificacionTemporal = "ANTECEDENTE_DOCUMENTADO";
                    dto.Detalle.Add(d);
                }
                continue;
            }

            var generales = compatibles.Where(r => !r.RequiereVerificacion &&
                (string.IsNullOrWhiteSpace(r.GrupoPoblacional) ||
                 GruposGenerales.Contains(r.GrupoPoblacional.Trim()))).ToList();
            var condicionales = compatibles.Except(generales).ToList();
            var aplicacion = aplicaciones.Where(a => a.VacunaId == grupo.Key.VacunaId &&
                a.NumeroDosis == grupo.Key.NumeroDosis)
                .OrderBy(a => a.FechaAplicacion).FirstOrDefault();

            // Registrar dosis efectivamente aplicada sin atribuirla automáticamente
            // al cumplimiento de la etapa de vida actual.
            if (aplicacion is not null)
            {
                var referencia = generales.FirstOrDefault() ?? compatibles[0];
                var aplicada = Nuevo(referencia, "APLICADA", "Antecedente documentado hasta la fecha de corte.");
                aplicada.FechaAplicacion = aplicacion.FechaAplicacion;
                aplicada.ClasificacionTemporal = "ANTECEDENTE_DOCUMENTADO";
                dto.Detalle.Add(aplicada);
                continue;
            }

            if (generales.Count == 0)
            {
                if (condicionales.Any(r => VentanaActual(r, edadDias)))
                {
                    var item = Nuevo(condicionales.First(r => VentanaActual(r, edadDias)),
                        "VERIFICAR", "Condición poblacional o clínica sin confirmar.");
                    item.ClasificacionTemporal = "ETAPA_ACTUAL";
                    dto.Detalle.Add(item);
                }
                continue;
            }

            // Las ventanas alternativas de la misma vacuna y número de dosis
            // no se contabilizan como múltiples dosis.
            var actuales = generales.Where(r => VentanaActual(r, edadDias)).ToList();
            var futuras = generales.Where(r => r.EdadMinDias.HasValue && r.EdadMinDias.Value > edadDias)
                .OrderBy(r => r.EdadMinDias).ThenBy(r => r.ReglaId).ToList();
            var historicas = generales.Where(r => r.EdadMaxDias.HasValue && r.EdadMaxDias.Value < edadDias).ToList();

            if (actuales.Count == 0 && futuras.Count == 0 && historicas.Count == 0)
            {
                var item = Nuevo(generales[0], "VERIFICAR", "Regla sin ventana de edad suficiente para clasificarla.");
                item.ClasificacionTemporal = "SIN_PARAMETRIZACION";
                dto.Detalle.Add(item);
                continue;
            }

            if (actuales.Count == 0 && futuras.Count == 0)
            {
                // Una ventana infantil expirada NO equivale a una obligación actual.
                var regla = historicas.OrderByDescending(r => r.EdadMaxDias).First();
                var validada = regla.ReglaValidada && regla.VigenteDesde.HasValue &&
                    regla.CohorteDesde.HasValue;
                var item = Nuevo(regla, validada ? "HISTORICA_NO_DOCUMENTADA" : "HISTORICA_NO_VERIFICABLE",
                    validada ? "Regla histórica validada para cohorte; falta antecedente documentado, no implica rescate." :
                    "Catálogo sin vigencia histórica/cohorte validada; no se presume obligación retrospectiva.");
                item.ClasificacionTemporal = validada ? "ETAPA_HISTORICA_VALIDADA" : "ETAPA_HISTORICA_NO_VALIDADA";
                Fechas(item, regla, nacimiento);
                dto.Detalle.Add(item);
                continue;
            }

            if (actuales.Count == 0)
            {
                var regla = futuras[0];
                var item = Nuevo(regla, "FUTURA", "La ventana de edad comienza después de la fecha de corte.");
                item.ClasificacionTemporal = "ETAPA_FUTURA";
                Fechas(item, regla, nacimiento);
                dto.Detalle.Add(item);
                continue;
            }

            var vigente = actuales.OrderBy(r => r.EdadMinDias ?? int.MaxValue)
                .ThenBy(r => r.ReglaId).First();
            var actual = Nuevo(vigente, vigente.ReglaValidada ? "PENDIENTE" : "VERIFICAR",
                vigente.ReglaValidada ? "Dosis en ventana actual de regla validada sin aplicación documentada." :
                "Regla actual pendiente de validación de vigencia, cohorte e indicación.");
            actual.ClasificacionTemporal = "ETAPA_ACTUAL";
            Fechas(actual, vigente, nacimiento);

            if (actuales.Any(r => !r.EdadMinDias.HasValue))
            {
                actual.Estado = "VERIFICAR";
                actual.Motivo = "La ventana actual no tiene edad mínima parametrizada.";
            }
            else if (grupo.Key.NumeroDosis > 1)
            {
                var anterior = aplicaciones.Where(a => a.VacunaId == grupo.Key.VacunaId &&
                    a.NumeroDosis == grupo.Key.NumeroDosis - 1)
                    .OrderByDescending(a => a.FechaAplicacion).FirstOrDefault();
                if (anterior is null)
                {
                    actual.Estado = "VERIFICAR";
                    actual.Motivo = "Falta documentar dosis anterior; no se determina automáticamente esquema de rescate.";
                }
                else if (!vigente.IntervaloDiasMin.HasValue)
                {
                    actual.Estado = "VERIFICAR";
                    actual.Motivo = "Intervalo mínimo no parametrizado.";
                }
                else
                {
                    var fechaIntervalo = anterior.FechaAplicacion.Date.AddDays(vigente.IntervaloDiasMin.Value);
                    if (fechaIntervalo > corte)
                    {
                        actual.Estado = "FUTURA";
                        actual.FechaMinima = fechaIntervalo;
                        actual.Motivo = "Intervalo mínimo todavía no cumplido.";
                    }
                }
            }
            dto.Detalle.Add(actual);
        }

        dto.DosisHistoricasNoDocumentadas = dto.Detalle.Count(x => x.Estado == "HISTORICA_NO_DOCUMENTADA");
        dto.DosisExigibles = dto.Detalle.Count(x => x.Estado == "PENDIENTE");
        dto.DosisAplicadasDocumentadas = aplicaciones.Count;
        // No se atribuye una aplicación a una obligación de etapa sin reglas
        // explícitas de equivalencia y serie validada.
        dto.DosisExigiblesAplicadas = 0;
        dto.DosisExigiblesActualesAplicadas = 0;
        dto.DosisExigiblesActuales = dto.DosisExigibles;
        dto.DosisPendientes = dto.Detalle.Count(x => x.Estado == "PENDIENTE");
        dto.DosisVencidas = 0; // En V2 no se infiere rezago exigible de ventanas históricas.
        dto.DosisFuturas = dto.Detalle.Count(x => x.Estado == "FUTURA");
        dto.DosisRequierenVerificacion = dto.Detalle.Count(x => x.Estado == "VERIFICAR");

        // Sin obligatoriedad por etapa, equivalencias y antecedentes verificables,
        // ningún resultado positivo puede certificar cumplimiento.
        dto.EsEvaluable = false;
        dto.AlCorrienteParaEdad = null;
        dto.EsquemaCompletoEtapa = false;
        dto.Estado = "NO_EVALUABLE";
        dto.Advertencias.Add("V3 exige metadatos validados de cohorte/vigencia para interpretar reglas; no certifica cumplimiento ni cobertura oficial.");
        dto.Advertencias.Add("Las alternativas de biológicos, esquemas de rescate y obligatoriedad por etapa requieren reglas clínicas explícitas.");
        dto.Advertencias.Add("DosisAplicadasDocumentadas registra antecedentes; DosisExigiblesActualesAplicadas permanece en 0 hasta validar correspondencia clínica.");
        if (dto.Detalle.Any(x => x.Estado == "HISTORICA_NO_VERIFICABLE"))
            dto.Advertencias.Add("Existen reglas históricas sin vigencia/cohorte validada: no son incumplimientos demostrados.");
        if (reglas.Any(r => !string.IsNullOrWhiteSpace(r.GrupoEquivalencia)))
            dto.Advertencias.Add("Hay grupos de equivalencia catalogados; aún no se presume sustitución de dosis sin reglas clínicas de serie.");
        return dto;
    }

    private static bool VentanaActual(Regla r, int edadDias) =>
        r.EdadMinDias.HasValue && r.EdadMinDias.Value <= edadDias &&
        (!r.EdadMaxDias.HasValue || r.EdadMaxDias.Value >= edadDias);

    private static void Fechas(DetalleCoberturaNominalDto item, Regla r, DateTime nacimiento)
    {
        item.FechaMinima = r.EdadMinDias.HasValue ? nacimiento.AddDays(r.EdadMinDias.Value) : null;
        item.FechaMaxima = r.EdadMaxDias.HasValue ? nacimiento.AddDays(r.EdadMaxDias.Value) : null;
    }

    private static DetalleCoberturaNominalDto Nuevo(Regla r, string estado, string motivo) => new()
    {
        VacunaId = r.VacunaId, Vacuna = r.Vacuna ?? string.Empty,
        NumeroDosis = r.NumeroDosis, Estado = estado, Motivo = motivo
    };

    private sealed class Paciente
    {
        public Guid Id { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string? Sexo { get; set; }
    }
    private sealed class Regla
    {
        public int ReglaId { get; set; }
        public int VacunaId { get; set; }
        public string? Vacuna { get; set; }
        public int NumeroDosis { get; set; }
        public string? TipoDosis { get; set; }
        public string? GrupoPoblacional { get; set; }
        public string? Sexo { get; set; }
        public int? EdadMinDias { get; set; }
        public int? EdadMaxDias { get; set; }
        public bool RequiereVerificacion { get; set; }
        public int? IntervaloDiasMin { get; set; }
        public DateTime? VigenteDesde { get; set; }
        public DateTime? VigenteHasta { get; set; }
        public DateTime? CohorteDesde { get; set; }
        public DateTime? CohorteHasta { get; set; }
        public bool ReglaValidada { get; set; }
        public string? GrupoEquivalencia { get; set; }
    }
    private sealed class Aplicacion
    {
        public int VacunaId { get; set; }
        public int NumeroDosis { get; set; }
        public DateTime FechaAplicacion { get; set; }
    }
}
