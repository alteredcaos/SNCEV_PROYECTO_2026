using Dapper;
using Microsoft.Data.SqlClient;
using SNCEV.Api.Models;

namespace SNCEV.Api.Services;

public sealed class MotorEsquemaVacunacionService
{
    private readonly IConfiguration _config;

    public MotorEsquemaVacunacionService(IConfiguration config)
    {
        _config = config;
    }

    private string GetConnectionString()
    {
        return _config.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException(
                "No se encontró la cadena de conexión 'SqlServerConnection'.");
    }
    private static bool EsReglaGeneral(ReglaDb regla)
    {
        if (regla.RequiereVerificacion)
            return false;

        if (string.IsNullOrWhiteSpace(regla.GrupoPoblacional))
            return true;

        var grupo = regla.GrupoPoblacional
            .Trim()
            .ToUpperInvariant();

        return grupo switch
        {
            "POBLACION_GENERAL" => true,
            "INFANTIL" => true,
            "NINOS" => true,
            "RECIEN_NACIDO" => true,
            "SR_INFANTIL" => true,
            "SRP_INFANTIL" => true,
            "INFANTIL_Y_ADULTO_MAYOR" => true,

            _ => false
        };
    }
    private static bool GrupoCompatibleConSexo(
    ReglaDb regla,
    string sexoPaciente)
    {
        var grupo = (regla.GrupoPoblacional ?? string.Empty)
            .Trim()
            .ToUpperInvariant();

        if (grupo.StartsWith("EMBARAZADA") &&
            !string.Equals(
                sexoPaciente,
                "F",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
    public async Task<EsquemaVacunacionDto?> EvaluarAsync(
      Guid pacienteId,
      CancellationToken cancellationToken = default,
      DateTime? fechaEvaluacion = null)
    {
        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync(cancellationToken);

        // ---------------------------------------------------------
        // 1. Obtener paciente activo
        // ---------------------------------------------------------

        var paciente = await conn.QueryFirstOrDefaultAsync<PacienteMotorDb>(
            new CommandDefinition(
                @"
                SELECT
                    id AS Id,
                    nombre AS Nombre,
                    primer_apellido AS PrimerApellido,
                    segundo_apellido AS SegundoApellido,
                    fecha_nacimiento AS FechaNacimiento,
                    sexo AS Sexo
                FROM sncev.paciente
                WHERE id = @PacienteId
                  AND eliminado = 0;
                ",
                new { PacienteId = pacienteId },
                cancellationToken: cancellationToken));

        if (paciente is null)
            return null;

        var hoy = (fechaEvaluacion ?? DateTime.Today).Date;

        var edad = CalcularEdad(
            paciente.FechaNacimiento.Date,
            hoy);

        // ---------------------------------------------------------
        // 2. Aplicaciones existentes
        // ---------------------------------------------------------

        var aplicaciones = (
            await conn.QueryAsync<AplicacionDb>(
                new CommandDefinition(
                    @"
                    SELECT
                        va.id AS AplicacionId,
                        va.vacuna_id AS VacunaId,
                        vc.clave_anx AS ClaveAnx,
                        vc.nombre_generico AS Vacuna,
                        va.numero_dosis AS NumeroDosis,
                        va.fecha_aplicacion AS FechaAplicacion,
                        va.lote AS Lote,
                        va.laboratorio AS Laboratorio,
                        va.unidad_medica_clues AS UnidadMedicaClues,
                        va.grupo_poblacional AS GrupoPoblacional,
                        va.es_esquema_completo AS EsEsquemaCompleto
                    FROM sncev.vacuna_aplicada va
                    INNER JOIN sncev.vacuna_catalogo vc
                        ON vc.id = va.vacuna_id
                    WHERE va.paciente_id = @PacienteId
                      AND va.anulada = 0
                    ORDER BY
                        va.fecha_aplicacion,
                        va.numero_dosis;
                    ",
                    new { PacienteId = pacienteId },
                    cancellationToken: cancellationToken)))
            .ToList();

        // ---------------------------------------------------------
        // 3. Cargar reglas + catálogo
        // ---------------------------------------------------------

        var reglas = (
            await conn.QueryAsync<ReglaDb>(
                new CommandDefinition(
                    @"
                    SELECT
                        r.id AS ReglaId,

                        vc.id AS VacunaId,

                        r.vacuna_clave_anx AS ClaveAnx,

                        COALESCE(
                            vc.nombre_generico,
                            r.vacuna_nombre
                        ) AS Vacuna,

                        r.numero_dosis AS NumeroDosis,
                        r.tipo_dosis AS TipoDosis,
                        r.grupo_poblacional AS GrupoPoblacional,
                        r.sexo AS Sexo,

                        r.edad_texto_original AS EdadTexto,
                        r.edad_min_dias AS EdadMinDias,
                        r.edad_max_dias AS EdadMaxDias,

                        r.observaciones AS Observaciones,

                        r.requiere_verificacion
                            AS RequiereVerificacion,

                        vc.intervalo_dias_min
                            AS IntervaloCatalogoDias

                    FROM sncev.regla_asignacion_biologico r

                    INNER JOIN sncev.vacuna_catalogo vc
                        ON vc.clave_anx = r.vacuna_clave_anx

                    WHERE vc.activo = 1

                    ORDER BY
                        vc.id,
                        r.numero_dosis,
                        r.edad_min_dias,
                        r.id;
                    ",
                    cancellationToken: cancellationToken)))
            .ToList();

        // ---------------------------------------------------------
        // 4. DTO principal
        // ---------------------------------------------------------

        var resultado = new EsquemaVacunacionDto
        {
            PacienteId = paciente.Id,

            NombrePaciente = ConstruirNombre(paciente),

            FechaNacimiento = paciente.FechaNacimiento,

            Sexo = paciente.Sexo,

            Edad = edad,

            FechaEvaluacion = hoy,

            Aplicadas = aplicaciones
                .Select(MapAplicacion)
                .ToList()
        };

        // ---------------------------------------------------------
        // 5. Evaluar reglas
        //
        // IMPORTANTE:
        //
        // Una fila de regla NO equivale necesariamente a una dosis.
        // Puede haber múltiples ventanas de edad para:
        //
        //     ANX003 / dosis 1
        //
        // Por eso primero agrupamos.
        // ---------------------------------------------------------

        var grupos = reglas
            .Where(r => r.NumeroDosis.HasValue)
            .GroupBy(r => new
            {
                r.VacunaId,
                r.ClaveAnx,
                r.NumeroDosis
            });

        foreach (var grupo in grupos)
        {
            var numeroDosis = grupo.Key.NumeroDosis!.Value;

            // -----------------------------------------------------
            // ¿Ya está aplicada?
            // -----------------------------------------------------

            var yaAplicada = aplicaciones.Any(a =>
                a.VacunaId == grupo.Key.VacunaId &&
                a.NumeroDosis == numeroDosis);

            if (yaAplicada)
                continue;

            // -----------------------------------------------------
            // Sexo
            // -----------------------------------------------------

                        var reglasSexo = grupo
                 .Where(r =>
                     (
                         string.IsNullOrWhiteSpace(r.Sexo) ||
                         string.Equals(
                             r.Sexo.Trim(),
                             paciente.Sexo.Trim(),
                             StringComparison.OrdinalIgnoreCase)
                     )
                     &&
                     GrupoCompatibleConSexo(
                         r,
                         paciente.Sexo)
                 )
                 .ToList();

            if (reglasSexo.Count == 0)
                continue;

            // -----------------------------------------------------
            // Reglas cuya edad coincide HOY
            // -----------------------------------------------------

            var reglasEdadActual = reglasSexo
                   .Where(r =>
                       EdadCoincide(
                           edad.TotalDias,
                           r.EdadMinDias,
                           r.EdadMaxDias))
                   .ToList();

            // Separar reglas generales de reglas que dependen
            // de una condición/grupo poblacional no conocido.
            var reglasGenerales = reglasEdadActual
                .Where(EsReglaGeneral)
                .ToList();

            var reglasCondicionales = reglasEdadActual
                .Where(r => !EsReglaGeneral(r))
                .ToList();

            // -----------------------------------------------------
            // Hay una regla vigente por edad
            // -----------------------------------------------------
            if (reglasGenerales.Count > 0)
            {
                var regla =
                    SeleccionarMejorRegla(reglasGenerales);


                // -------------------------------------------------
                // Comprobar intervalo con dosis anterior.
                // -------------------------------------------------

                var evaluacionIntervalo =
                    EvaluarIntervalo(
                        regla,
                        aplicaciones,
                        hoy);

                if (!evaluacionIntervalo.Cumplido)
                {
                    var estado =
                        evaluacionIntervalo.FaltaDosisAnterior
                            ? "PENDIENTE_DOSIS_ANTERIOR"
                            : "PROGRAMADA";

                    resultado.Programadas.Add(
                        CrearDosis(
                            regla,
                            estado,
                            evaluacionIntervalo.Motivo,
                            evaluacionIntervalo.FechaMinima));

                    continue;
                }

                resultado.Aplicables.Add(
                    CrearDosis(
                        regla,
                        "APLICABLE",
                        ConstruirMotivoAplicable(regla),
                        evaluacionIntervalo.FechaMinima));

                continue;
            }

            // -----------------------------------------------------
            // Todavía no llega a la edad mínima.
            // Sólo mostramos la siguiente ventana futura.
            // -----------------------------------------------------

            //       var siguiente = reglasSexo
            //.Where(EsReglaGeneral)
            //.Where(r =>
            //    r.EdadMinDias.HasValue &&
            //    edad.TotalDias < r.EdadMinDias.Value)
            //.OrderBy(r => r.EdadMinDias)
            //.ThenBy(r => PrioridadTipoDosis(r.TipoDosis))
            //.ThenBy(r => r.ReglaId)
            //.FirstOrDefault();

            //       if (siguiente is not null)
            //       {
            //           var fechaProgramada =
            //               paciente.FechaNacimiento.Date
            //                   .AddDays(siguiente.EdadMinDias!.Value);

            //           resultado.Programadas.Add(
            //               CrearDosis(
            //                   siguiente,
            //                   "PROGRAMADA",
            //                   $"Corresponde a partir de {siguiente.EdadTexto ?? $"{siguiente.EdadMinDias} días de edad"}.",
            //                   fechaProgramada));
            //       }
            //   }

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            var siguiente = reglasSexo
                    .Where(EsReglaGeneral)
                    .Where(r =>
                        r.EdadMinDias.HasValue &&
                        edad.TotalDias < r.EdadMinDias.Value)
                    .OrderBy(r => r.EdadMinDias)
                    .ThenBy(r => PrioridadTipoDosis(r.TipoDosis))
                    .ThenBy(r => r.ReglaId)
                    .FirstOrDefault();

            if (siguiente is not null)
            {
                var fechaPorEdad =
                    paciente.FechaNacimiento.Date
                        .AddDays(siguiente.EdadMinDias!.Value);

                // -----------------------------------------------------
                // Dosis 0 y dosis 1 no dependen de una dosis anterior.
                // -----------------------------------------------------

                if (!siguiente.NumeroDosis.HasValue ||
                    siguiente.NumeroDosis.Value <= 1)
                {
                    resultado.Programadas.Add(
                        CrearDosis(
                            siguiente,
                            "PROGRAMADA",
                            $"Corresponde a partir de {siguiente.EdadTexto ?? $"{siguiente.EdadMinDias} días de edad"}.",
                            fechaPorEdad));

                    continue;
                }

                // -----------------------------------------------------
                // Para D2, D3, D4... debe existir la dosis anterior.
                // -----------------------------------------------------

                var numeroDosisAnterior =
                    siguiente.NumeroDosis.Value - 1;

                var dosisAnterior = aplicaciones
                    .Where(a =>
                        a.VacunaId == siguiente.VacunaId &&
                        a.NumeroDosis == numeroDosisAnterior)
                    .OrderByDescending(a => a.FechaAplicacion)
                    .FirstOrDefault();

                if (dosisAnterior is null)
                {
                    resultado.Programadas.Add(
                        CrearDosis(
                            siguiente,
                            "PENDIENTE_DOSIS_ANTERIOR",
                            $"Primero debe registrarse la dosis {numeroDosisAnterior}.",
                            null));

                    continue;
                }

                // -----------------------------------------------------
                // Ya existe la dosis anterior.
                //
                // Si tenemos intervalo parametrizado:
                // fecha mínima = fecha dosis anterior + intervalo.
                //
                // Si no tenemos intervalo:
                // usamos únicamente la fecha de edad como proyección,
                // sin inventar un intervalo clínico.
                // -----------------------------------------------------

                DateTime fechaProgramada;

                if (siguiente.IntervaloCatalogoDias.HasValue)
                {
                    var fechaPorIntervalo =
                        dosisAnterior.FechaAplicacion.Date
                            .AddDays(
                                siguiente.IntervaloCatalogoDias.Value);

                    fechaProgramada =
                        fechaPorIntervalo > fechaPorEdad
                            ? fechaPorIntervalo
                            : fechaPorEdad;
                }
                else
                {
                    fechaProgramada = fechaPorEdad;
                }

                resultado.Programadas.Add(
                    CrearDosis(
                        siguiente,
                        "PROGRAMADA",
                        siguiente.IntervaloCatalogoDias.HasValue
                            ? $"Dosis anterior registrada. Fecha mínima estimada: {fechaProgramada:yyyy-MM-dd}."
                            : $"Dosis anterior registrada. Corresponde por edad a partir de {siguiente.EdadTexto ?? $"{siguiente.EdadMinDias} días"}. El intervalo mínimo no está parametrizado.",
                        fechaProgramada));
            }

        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // ---------------------------------------------------------
        // 6. Evitar duplicados producidos por reglas alternativas
        // ---------------------------------------------------------

        resultado.Aplicables =
            Deduplicar(resultado.Aplicables);

        resultado.Programadas =
            Deduplicar(resultado.Programadas);

        resultado.RequierenVerificacion =
            Deduplicar(resultado.RequierenVerificacion);

        // ---------------------------------------------------------
        // 7. Recomendación automática
        //
        // ÚNICAMENTE sale de APLICABLE.
        // Nunca seleccionamos automáticamente una regla clínica
        // que requiera verificación.
        // ---------------------------------------------------------

        var recomendada = resultado.Aplicables
            .OrderBy(x => x.EdadMinDias ?? int.MaxValue)
            .ThenBy(x => x.VacunaId)
            .ThenBy(x => x.NumeroDosis)
            .FirstOrDefault();

                if (recomendada is not null)
                {
                    resultado.Recomendacion =
                        new RecomendacionVacunacionDto
                        {
                            VacunaId = recomendada.VacunaId,
                            ClaveAnx = recomendada.ClaveAnx,
                            Vacuna = recomendada.Vacuna,
                            NumeroDosis = recomendada.NumeroDosis,
                            TipoDosis = recomendada.TipoDosis,
                            Estado = recomendada.Estado,
                            Motivo = recomendada.Motivo,
                            FechaMinimaAplicacion =
                                recomendada.FechaMinimaAplicacion
                        };
                }

                 return resultado;
        
    }

    // =============================================================
    // REGLAS DEL MOTOR
    // =============================================================

    private static bool EdadCoincide(
        int edadDias,
        int? minimo,
        int? maximo)
    {
        // Una regla sin edad explícita NO se considera
        // automáticamente aplicable.
        if (!minimo.HasValue && !maximo.HasValue)
            return false;

        if (minimo.HasValue && edadDias < minimo.Value)
            return false;

        if (maximo.HasValue && edadDias > maximo.Value)
            return false;

        return true;
    }

    private static ReglaDb SeleccionarMejorRegla(
        List<ReglaDb> reglas)
    {
        // Prioridad conservadora:
        //
        // REGULAR
        // UNICA
        // REFUERZO
        // REZAGADO
        // otras

        return reglas
            .OrderBy(r => PrioridadTipoDosis(r.TipoDosis))
            .ThenBy(r =>
                string.IsNullOrWhiteSpace(r.GrupoPoblacional)
                    ? 0
                    : 1)
            .ThenBy(r => r.ReglaId)
            .First();
    }

    private static int PrioridadTipoDosis(string? tipo)
    {
        return (tipo ?? string.Empty)
            .Trim()
            .ToUpperInvariant() switch
        {
            "REGULAR" => 1,
            "UNICA" => 2,
            "REFUERZO" => 3,
            "REZAGADO" => 4,
            "REVACUNACION" => 5,
            "DOSIS_CERO" => 6,
            "ADICIONAL" => 7,
            "SIN_REGLA_EDAD" => 8,
            _ => 99
        };
    }

    private static bool RequiereVerificacionClinica(
        ReglaDb regla)
    {
        if (regla.RequiereVerificacion)
            return true;

        if (string.IsNullOrWhiteSpace(
                regla.GrupoPoblacional))
            return false;

        var grupo = regla.GrupoPoblacional
            .Trim()
            .ToUpperInvariant();

        // Grupos que por sí mismos no agregan una condición
        // clínica especial en esta V1.
        var gruposGenerales = new HashSet<string>
        {
            "POBLACION_GENERAL",
            "INFANTIL",
            "NINOS",
            "RECIEN_NACIDO",
            "SR_INFANTIL",
            "SRP_INFANTIL",
            "INFANTIL_Y_ADULTO_MAYOR"
        };

        return !gruposGenerales.Contains(grupo);
    }

    private static EvaluacionIntervalo EvaluarIntervalo(
        ReglaDb regla,
        List<AplicacionDb> aplicaciones,
        DateTime hoy)
    {
        // Dosis cero, dosis 1 o reglas sin dosis anterior:
        if (!regla.NumeroDosis.HasValue ||
            regla.NumeroDosis.Value <= 1)
        {
            return new EvaluacionIntervalo
            {
                Cumplido = true
            };
        }

        var numeroAnterior =
            regla.NumeroDosis.Value - 1;

        var anterior = aplicaciones
            .Where(a =>
                a.VacunaId == regla.VacunaId &&
                a.NumeroDosis == numeroAnterior)
            .OrderByDescending(a => a.FechaAplicacion)
            .FirstOrDefault();

        // Si no está la dosis previa, esta dosis no debe
        // recomendarse automáticamente.
        if (anterior is null)
        {
            return new EvaluacionIntervalo
            {
                Cumplido = false,
                FaltaDosisAnterior = true,
                Motivo =
                    $"Requiere antecedente de dosis {numeroAnterior}."
            };
        }

        // Actualmente el catálogo tiene muchos intervalos NULL.
        // En ese caso NO inventamos un intervalo.
        if (!regla.IntervaloCatalogoDias.HasValue)
        {
            return new EvaluacionIntervalo
            {
                Cumplido = false,
                Motivo =
                    "El intervalo mínimo entre dosis no está parametrizado; requiere verificación."
            };
        }

        var fechaMinima =
            anterior.FechaAplicacion.Date
                .AddDays(regla.IntervaloCatalogoDias.Value);

        return new EvaluacionIntervalo
        {
            Cumplido = hoy >= fechaMinima,

            FechaMinima = fechaMinima,

            Motivo = hoy >= fechaMinima
                ? "Intervalo mínimo cumplido."
                : $"Aplicable a partir del {fechaMinima:yyyy-MM-dd}."
        };
    }

    // =============================================================
    // CREACIÓN DE DTO
    // =============================================================

    private static DosisAplicadaDto MapAplicacion(
        AplicacionDb a)
    {
        return new DosisAplicadaDto
        {
            AplicacionId = a.AplicacionId,
            VacunaId = a.VacunaId,
            ClaveAnx = a.ClaveAnx ?? string.Empty,
            Vacuna = a.Vacuna ?? string.Empty,
            NumeroDosis = a.NumeroDosis,
            FechaAplicacion = a.FechaAplicacion,
            Lote = a.Lote ?? string.Empty,
            Laboratorio = a.Laboratorio,
            UnidadMedicaClues =
                a.UnidadMedicaClues ?? string.Empty,
            GrupoPoblacional =
                a.GrupoPoblacional,
            EsEsquemaCompleto =
                a.EsEsquemaCompleto
        };
    }

    private static DosisEsquemaDto CrearDosis(
        ReglaDb regla,
        string estado,
        string motivo,
        DateTime? fechaMinima)
    {
        return new DosisEsquemaDto
        {
            ReglaId = regla.ReglaId,
            VacunaId = regla.VacunaId,
            ClaveAnx = regla.ClaveAnx ?? string.Empty,
            Vacuna = regla.Vacuna ?? string.Empty,
            NumeroDosis = regla.NumeroDosis ?? 0,
            TipoDosis = regla.TipoDosis ?? string.Empty,
            Estado = estado,
            GrupoPoblacional =
                regla.GrupoPoblacional,
            EdadTexto = regla.EdadTexto,
            EdadMinDias = regla.EdadMinDias,
            EdadMaxDias = regla.EdadMaxDias,
            IntervaloDiasMin =
                regla.IntervaloCatalogoDias,
            FechaMinimaAplicacion =
                fechaMinima,
            Motivo = motivo,
            Observaciones =
                regla.Observaciones,
            ReglaMarcadaParaVerificacion =
                regla.RequiereVerificacion
        };
    }

    private static string ConstruirMotivoAplicable(
        ReglaDb regla)
    {
        if (!string.IsNullOrWhiteSpace(
                regla.EdadTexto))
        {
            return
                $"Corresponde por edad ({regla.EdadTexto}) según la regla del esquema.";
        }

        return
            "Cumple los criterios parametrizados del esquema.";
    }

    private static string ConstruirMotivoVerificacion(
        ReglaDb regla)
    {
        if (!string.IsNullOrWhiteSpace(
                regla.GrupoPoblacional))
        {
            return
                $"La regla corresponde al grupo '{regla.GrupoPoblacional}' y debe confirmarse antes de recomendar la aplicación.";
        }

        return
            "La regla requiere verificación antes de recomendar la aplicación.";
    }

    private static List<DosisEsquemaDto> Deduplicar(
        IEnumerable<DosisEsquemaDto> items)
    {
        return items
            .GroupBy(x => new
            {
                x.VacunaId,
                x.NumeroDosis,
                x.Estado
            })
            .Select(g => g
                .OrderBy(x => x.EdadMinDias ?? int.MaxValue)
                .ThenBy(x => x.ReglaId)
                .First())
            .OrderBy(x => x.Vacuna)
            .ThenBy(x => x.NumeroDosis)
            .ToList();
    }

    // =============================================================
    // EDAD
    // =============================================================

    private static EdadPacienteDto CalcularEdad(
        DateTime nacimiento,
        DateTime hoy)
    {
        if (nacimiento > hoy)
            throw new InvalidOperationException(
                "La fecha de nacimiento no puede ser futura.");

        var anios = hoy.Year - nacimiento.Year;

        if (nacimiento.AddYears(anios) > hoy)
            anios--;

        var fechaTrasAnios =
            nacimiento.AddYears(anios);

        var meses = 0;
        var cursor = fechaTrasAnios;

        while (cursor.AddMonths(1) <= hoy)
        {
            cursor = cursor.AddMonths(1);
            meses++;
        }

        var dias =
            (hoy - cursor).Days;

        var totalDias =
            (hoy - nacimiento).Days;

        return new EdadPacienteDto
        {
            Anios = anios,
            Meses = meses,
            Dias = dias,
            TotalDias = totalDias,
            Texto =
                $"{anios} años, {meses} meses, {dias} días"
        };
    }

    private static string ConstruirNombre(
        PacienteMotorDb paciente)
    {
        return string.Join(
            " ",
            new[]
            {
                paciente.Nombre,
                paciente.PrimerApellido,
                paciente.SegundoApellido
            }
            .Where(x =>
                !string.IsNullOrWhiteSpace(x)));
    }

    // =============================================================
    // CLASES INTERNAS PARA DAPPER
    // =============================================================

    private sealed class PacienteMotorDb
    {
        public Guid Id { get; set; }

        public string Nombre { get; set; } =
            string.Empty;

        public string PrimerApellido { get; set; } =
            string.Empty;

        public string? SegundoApellido { get; set; }

        public DateTime FechaNacimiento { get; set; }

        public string Sexo { get; set; } =
            string.Empty;
    }

    private sealed class AplicacionDb
    {
        public Guid AplicacionId { get; set; }

        public int VacunaId { get; set; }

        public string? ClaveAnx { get; set; }

        public string? Vacuna { get; set; }

        public int NumeroDosis { get; set; }

        public DateTime FechaAplicacion { get; set; }

        public string? Lote { get; set; }

        public string? Laboratorio { get; set; }

        public string? UnidadMedicaClues { get; set; }

        public string? GrupoPoblacional { get; set; }

        public bool EsEsquemaCompleto { get; set; }
    }

    private sealed class ReglaDb
    {
        public int ReglaId { get; set; }

        public int VacunaId { get; set; }

        public string? ClaveAnx { get; set; }

        public string? Vacuna { get; set; }

        public int? NumeroDosis { get; set; }

        public string? TipoDosis { get; set; }

        public string? GrupoPoblacional { get; set; }

        public string? Sexo { get; set; }

        public string? EdadTexto { get; set; }

        public int? EdadMinDias { get; set; }

        public int? EdadMaxDias { get; set; }

        public string? Observaciones { get; set; }

        public bool RequiereVerificacion { get; set; }

        public int? IntervaloCatalogoDias { get; set; }
    }

    private sealed class EvaluacionIntervalo
    {
        public bool Cumplido { get; set; }

        public DateTime? FechaMinima { get; set; }

        public string Motivo { get; set; } =
            string.Empty;
        public bool FaltaDosisAnterior { get; set; }
    }
}