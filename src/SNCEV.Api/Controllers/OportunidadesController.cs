using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SNCEV.Api.Services;

namespace SNCEV.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/oportunidades-vacunacion")]
public sealed class OportunidadesController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly MotorEsquemaVacunacionService _motor;

    public OportunidadesController(
        IConfiguration config,
        MotorEsquemaVacunacionService motor)
    {
        _config = config;
        _motor = motor;
    }

    private string GetConnectionString()
    {
        return _config.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException(
                "No se encontró la cadena de conexión 'SqlServerConnection'.");
    }

    // ============================================================
    // GET /v1/oportunidades-vacunacion
    // ============================================================

    [HttpGet]
    [Authorize(Roles = "Administrador,Capturador,Directivo,Auditor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Obtener(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? estado = null,
        [FromQuery] string? ageb = null,
        [FromQuery] string? sexo = null,
        [FromQuery] bool? conCita = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            return BadRequest(new
            {
                message = "page debe ser mayor o igual a 1."
            });

        // Primera versión deliberadamente limitada.
        // El motor todavía se ejecuta paciente por paciente.
        if (pageSize < 1 || pageSize > 50)
            return BadRequest(new
            {
                message = "pageSize debe estar entre 1 y 50."
            });

        estado = estado?.Trim().ToUpperInvariant();
        sexo = sexo?.Trim().ToUpperInvariant();
        ageb = ageb?.Trim();

        if (!string.IsNullOrWhiteSpace(estado) &&
    estado is not (
        "APLICABLE" or
        "FUTURA" or
        "BLOQUEADA" or
        "VERIFICAR"))
        {
            return BadRequest(new
            {
                message =
                    "estado debe ser APLICABLE, FUTURA, BLOQUEADA o VERIFICAR."
            });
        }

        if (!string.IsNullOrWhiteSpace(sexo) &&
            sexo is not ("M" or "F"))
        {
            return BadRequest(new
            {
                message = "sexo debe ser M o F."
            });
        }

        await using var conn =
            new SqlConnection(GetConnectionString());

        await conn.OpenAsync(cancellationToken);

        // --------------------------------------------------------
        // 1. Pacientes activos
        //
        // La paginación se hace sobre pacientes, no sobre
        // oportunidades.
        // --------------------------------------------------------

        var offset = (page - 1) * pageSize;

        var pacientes = (
            await conn.QueryAsync<PacienteOportunidadDb>(
                new CommandDefinition(
                    """
                    SELECT
                        p.id AS PacienteId,
                        LTRIM(RTRIM(CONCAT(
                            p.nombre, ' ',
                            p.primer_apellido, ' ',
                            COALESCE(p.segundo_apellido, '')
                        ))) AS Paciente,
                        p.fecha_nacimiento AS FechaNacimiento,
                        p.sexo AS Sexo,

                        d.ageb_clave AS AgebClave,
                        d.cvegeo AS Cvegeo,
                        d.localidad AS Localidad,
                        d.colonia AS Colonia,
                        d.clues_sugerida AS CluesSugerida,

                        m.nombre AS Municipio,
                        e.nombre AS Estado

                    FROM sncev.paciente p

                    LEFT JOIN sncev.domicilio_paciente d
                        ON d.paciente_id = p.id
                       AND d.vigente = 1

                    LEFT JOIN sncev_geo.municipio m
                        ON m.id = d.municipio_id

                    LEFT JOIN sncev_geo.estado e
                        ON e.clave_inegi = d.estado_clave

                    WHERE p.eliminado = 0

                      AND (
                            @Sexo IS NULL
                            OR p.sexo = @Sexo
                          )

                      AND (
                            @Ageb IS NULL
                            OR d.ageb_clave = @Ageb
                          )

                    ORDER BY
                        p.primer_apellido,
                        p.segundo_apellido,
                        p.nombre,
                        p.id

                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY;
                    """,
                    new
                    {
                        Sexo = string.IsNullOrWhiteSpace(sexo)
                            ? null
                            : sexo,

                        Ageb = string.IsNullOrWhiteSpace(ageb)
                            ? null
                            : ageb,

                        Offset = offset,
                        PageSize = pageSize
                    },
                    cancellationToken: cancellationToken)))
            .ToList();

        // --------------------------------------------------------
        // 2. Citas activas de esos pacientes
        // --------------------------------------------------------

        var pacienteIds = pacientes
            .Select(x => x.PacienteId)
            .ToArray();

        var citasActivas = new List<CitaActivaDb>();

        if (pacienteIds.Length > 0)
        {
            citasActivas = (
                await conn.QueryAsync<CitaActivaDb>(
                    new CommandDefinition(
                        """
                        SELECT
                            c.id AS Id,
                            c.paciente_id AS PacienteId,
                            c.vacuna_id AS VacunaId,
                            c.numero_dosis AS NumeroDosis,
                            c.fecha_hora AS FechaHora,
                            c.clues AS Clues,
                            c.estado AS Estado

                        FROM sncev.cita_vacunacion c

                        WHERE c.paciente_id IN @PacienteIds
                          AND c.estado IN ('PROGRAMADA', 'CONFIRMADA')

                        ORDER BY c.fecha_hora;
                        """,
                        new
                        {
                            PacienteIds = pacienteIds
                        },
                        cancellationToken: cancellationToken)))
                .ToList();
        }

        // --------------------------------------------------------
        // 3. Evaluar motor
        // --------------------------------------------------------

        var oportunidades =
            new List<OportunidadVacunacionDto>();

        foreach (var paciente in pacientes)
        {
            var esquema = await _motor.EvaluarAsync(
                paciente.PacienteId,
                cancellationToken);

            if (esquema is null)
                continue;

            // APLICABLES
            // ========================================================
            // APLICABLES
            // Puede vacunarse actualmente.
            // ========================================================

            foreach (var dosis in esquema.Aplicables)
            {
                AgregarOportunidad(
                    oportunidades,
                    paciente,
                    dosis.VacunaId,
                    dosis.ClaveAnx,
                    dosis.Vacuna,
                    dosis.NumeroDosis,
                    "APLICABLE",
                    dosis.FechaMinimaAplicacion,
                    dosis.Motivo,
                    citasActivas);
            }


            // ========================================================
            // PROGRAMADAS DEL MOTOR
            //
            // Aquí traducimos el estado clínico del motor a un estado
            // operativo para Gestión de Demanda.
            //
            // Con fecha mínima  -> FUTURA
            // Sin fecha mínima  -> BLOQUEADA
            // ========================================================

            foreach (var dosis in esquema.Programadas)
            {
                string estadoOperativo;

                if (dosis.NumeroDosis <= 0)
                {
                    estadoOperativo = "VERIFICAR";
                }
                else if (dosis.FechaMinimaAplicacion.HasValue)
                {
                    estadoOperativo = "FUTURA";
                }
                else
                {
                    estadoOperativo = "BLOQUEADA";
                }

                AgregarOportunidad(
                    oportunidades,
                    paciente,
                    dosis.VacunaId,
                    dosis.ClaveAnx,
                    dosis.Vacuna,
                    dosis.NumeroDosis,
                    estadoOperativo,
                    dosis.FechaMinimaAplicacion,
                    dosis.Motivo,
                    citasActivas);
            }


            // ========================================================
            // REQUIEREN VERIFICACIÓN
            //
            // No deben tratarse automáticamente como aplicables.
            // Requieren revisión antes de generar una cita.
            // ========================================================

            foreach (var dosis in esquema.RequierenVerificacion)
            {
                AgregarOportunidad(
                    oportunidades,
                    paciente,
                    dosis.VacunaId,
                    dosis.ClaveAnx,
                    dosis.Vacuna,
                    dosis.NumeroDosis,
                    "VERIFICAR",
                    dosis.FechaMinimaAplicacion,
                    dosis.Motivo,
                    citasActivas);
            }
        }

        // --------------------------------------------------------
        // 4. Filtro por existencia de cita
        // --------------------------------------------------------

        IEnumerable<OportunidadVacunacionDto> resultado =
    oportunidades;


        // --------------------------------------------------------
        // Estado operativo
        // --------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(estado))
        {
            resultado = resultado.Where(
                x => x.EstadoOportunidad == estado);
        }


        // --------------------------------------------------------
        // Existencia de cita activa
        // --------------------------------------------------------

        if (conCita.HasValue)
        {
            resultado = resultado.Where(
                x => x.TieneCitaActiva == conCita.Value);
        }

        var items = resultado
            .OrderBy(x => x.TieneCitaActiva)
            .ThenBy(x => x.FechaMinimaAplicacion)
            .ThenBy(x => x.Paciente)
            .ThenBy(x => x.Vacuna)
            .ThenBy(x => x.NumeroDosis)
            .ToList();

        return Ok(new
        {
            fechaEvaluacion = DateTime.Today,
            page,
            pageSize,

            // Importante:
            // totalOportunidades corresponde a esta página
            // de pacientes, no al universo nacional.
            totalOportunidades = items.Count,

            resumen = new
            {
                sinCita = items.Count(x => !x.TieneCitaActiva),

                conCita = items.Count(x => x.TieneCitaActiva),

                aplicables = items.Count(
        x => x.EstadoOportunidad == "APLICABLE"),

                futuras = items.Count(
        x => x.EstadoOportunidad == "FUTURA"),

                bloqueadas = items.Count(
        x => x.EstadoOportunidad == "BLOQUEADA"),

                verificar = items.Count(
        x => x.EstadoOportunidad == "VERIFICAR")
            },

            items
        });
    }

    // ============================================================
    // Construcción de oportunidad
    // ============================================================

    private static void AgregarOportunidad(
        ICollection<OportunidadVacunacionDto> destino,
        PacienteOportunidadDb paciente,
        int vacunaId,
        string claveAnx,
        string vacuna,
        int numeroDosis,
        string estadoOportunidad,
        DateTime? fechaMinimaAplicacion,
        string motivo,
        IReadOnlyCollection<CitaActivaDb> citas)
    {
        var cita = citas
            .Where(c =>
                c.PacienteId == paciente.PacienteId &&
                c.VacunaId == vacunaId &&
                c.NumeroDosis == numeroDosis)
            .OrderBy(c => c.FechaHora)
            .FirstOrDefault();

        destino.Add(new OportunidadVacunacionDto
        {
            PacienteId = paciente.PacienteId,
            Paciente = paciente.Paciente,
            FechaNacimiento = paciente.FechaNacimiento,
            Sexo = paciente.Sexo,

            VacunaId = vacunaId,
            ClaveAnx = claveAnx,
            Vacuna = vacuna,
            NumeroDosis = numeroDosis,

            EstadoOportunidad = estadoOportunidad,
            FechaMinimaAplicacion = fechaMinimaAplicacion,
            Motivo = motivo,

            AgebClave = paciente.AgebClave,
            Cvegeo = paciente.Cvegeo,
            Localidad = paciente.Localidad,
            Colonia = paciente.Colonia,
            Municipio = paciente.Municipio,
            Estado = paciente.Estado,

            // Informativa, no significa adscripción.
            CluesSugerida = paciente.CluesSugerida,

            TieneCitaActiva = cita is not null,

            Cita = cita is null
                ? null
                : new CitaOportunidadDto
                {
                    Id = cita.Id,
                    FechaHora = cita.FechaHora,
                    Clues = cita.Clues,
                    Estado = cita.Estado
                }
        });
    }

    // ============================================================
    // Clases internas
    // ============================================================

    private sealed class PacienteOportunidadDb
    {
        public Guid PacienteId { get; set; }
        public string Paciente { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public string Sexo { get; set; } = string.Empty;

        public string? AgebClave { get; set; }
        public string? Cvegeo { get; set; }
        public string? Localidad { get; set; }
        public string? Colonia { get; set; }
        public string? CluesSugerida { get; set; }
        public string? Municipio { get; set; }
        public string? Estado { get; set; }
    }

    private sealed class CitaActivaDb
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public int VacunaId { get; set; }
        public int NumeroDosis { get; set; }
        public DateTime FechaHora { get; set; }
        public string Clues { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }

    private sealed class OportunidadVacunacionDto
    {
        public Guid PacienteId { get; set; }
        public string Paciente { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public string Sexo { get; set; } = string.Empty;

        public int VacunaId { get; set; }
        public string ClaveAnx { get; set; } = string.Empty;
        public string Vacuna { get; set; } = string.Empty;
        public int NumeroDosis { get; set; }

        public string EstadoOportunidad { get; set; } =
            string.Empty;

        public DateTime? FechaMinimaAplicacion { get; set; }

        public string Motivo { get; set; } = string.Empty;

        public string? AgebClave { get; set; }
        public string? Cvegeo { get; set; }
        public string? Localidad { get; set; }
        public string? Colonia { get; set; }
        public string? Municipio { get; set; }
        public string? Estado { get; set; }

        public string? CluesSugerida { get; set; }

        public bool TieneCitaActiva { get; set; }

        public CitaOportunidadDto? Cita { get; set; }
    }

    private sealed class CitaOportunidadDto
    {
        public Guid Id { get; set; }
        public DateTime FechaHora { get; set; }
        public string Clues { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }
}