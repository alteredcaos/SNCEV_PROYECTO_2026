using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SNCEV.Api.Services;
using System.Security.Claims;

namespace SNCEV.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/citas")]
public  class CitasController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly MotorEsquemaVacunacionService _motor;

    public CitasController(
        IConfiguration config,
        MotorEsquemaVacunacionService motor)
    {
        _config = config;
        _motor = motor;
    }

    // ============================================================
    // GET /v1/citas
    // ============================================================

    [HttpGet]
    [Authorize(Roles = "Administrador,Capturador,Directivo,Auditor")]
    public async Task<IActionResult> Obtener(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] string? clues,
        [FromQuery] string? estado,
        [FromQuery] Guid? pacienteId,
        CancellationToken cancellationToken)
    {
        var fechaDesde =
       desde?.ToDateTime(TimeOnly.MinValue)
       ?? DateTime.Today;

        var fechaHastaInclusiva =
            hasta?.ToDateTime(TimeOnly.MinValue)
            ?? fechaDesde.AddDays(30);
        
        var fechaHastaExclusiva = fechaHastaInclusiva.AddDays(1);
        
        if (fechaHastaInclusiva <= fechaDesde)
        {
            return BadRequest(new
            {
                message = "El rango de fechas no es válido."
            });
        }

        var cluesUsuario = User.FindFirstValue("clues");

        // Capturador trabaja sobre su propia CLUES.
        if (User.IsInRole("Capturador"))
        {
            if (string.IsNullOrWhiteSpace(cluesUsuario))
                return Forbid();

            clues = cluesUsuario;
        }

        await using var conn =
            new SqlConnection(GetConnectionString());

        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                c.id AS Id,
                c.paciente_id AS PacienteId,

                CONCAT(
                    p.nombre, ' ',
                    p.primer_apellido,
                    CASE
                        WHEN NULLIF(
                            LTRIM(RTRIM(p.segundo_apellido)),
                            ''
                        ) IS NULL
                            THEN ''
                        ELSE ' ' + p.segundo_apellido
                    END
                ) AS Paciente,

                p.fecha_nacimiento AS FechaNacimiento,
                p.sexo AS Sexo,

                c.vacuna_id AS VacunaId,
                vc.clave_anx AS ClaveAnx,
                vc.nombre_generico AS Vacuna,

                c.numero_dosis AS NumeroDosis,
                c.fecha_hora AS FechaHora,
                c.clues AS Clues,

                c.estado AS Estado,
                c.motivo AS Motivo,
                c.notas AS Notas,

                c.usuario_registro_id AS UsuarioRegistroId,
                c.usuario_registro_nombre AS UsuarioRegistroNombre,

                c.fecha_registro AS FechaRegistro,
                c.fecha_actualizacion AS FechaActualizacion

            FROM sncev.cita_vacunacion c

            INNER JOIN sncev.paciente p
                ON p.id = c.paciente_id

            INNER JOIN sncev.vacuna_catalogo vc
                ON vc.id = c.vacuna_id

            WHERE p.eliminado = 0

              AND c.fecha_hora >= @Desde
              AND c.fecha_hora < @Hasta

              AND
              (
                  @Clues IS NULL
                  OR c.clues = @Clues
              )

              AND
              (
                  @Estado IS NULL
                  OR c.estado = @Estado
              )

              AND
              (
                  @PacienteId IS NULL
                  OR c.paciente_id = @PacienteId
              )

            ORDER BY
                c.fecha_hora,
                p.primer_apellido,
                p.nombre;
            """;

        var citas = await conn.QueryAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Desde = fechaDesde,
                    Hasta = fechaHastaExclusiva,

                    Clues =
                        string.IsNullOrWhiteSpace(clues)
                            ? null
                            : clues.Trim(),

                    Estado =
                        string.IsNullOrWhiteSpace(estado)
                            ? null
                            : estado.Trim().ToUpperInvariant(),

                    PacienteId = pacienteId
                },
                cancellationToken: cancellationToken));

        return Ok(new
        {
            desde = DateOnly.FromDateTime(fechaDesde),
            hasta = DateOnly.FromDateTime(fechaHastaInclusiva),
            total = citas.Count(),
            items = citas
        });
    }

    // ============================================================
    // GET /v1/citas/{id}
    // ============================================================

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Administrador,Capturador,Directivo,Auditor")]
    public async Task<IActionResult> ObtenerPorId(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var conn =
            new SqlConnection(GetConnectionString());

        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                c.id AS Id,
                c.paciente_id AS PacienteId,

                CONCAT(
                    p.nombre, ' ',
                    p.primer_apellido,
                    CASE
                        WHEN NULLIF(
                            LTRIM(RTRIM(p.segundo_apellido)),
                            ''
                        ) IS NULL
                            THEN ''
                        ELSE ' ' + p.segundo_apellido
                    END
                ) AS Paciente,

                p.fecha_nacimiento AS FechaNacimiento,
                p.sexo AS Sexo,

                c.vacuna_id AS VacunaId,
                vc.clave_anx AS ClaveAnx,
                vc.nombre_generico AS Vacuna,

                c.numero_dosis AS NumeroDosis,
                c.fecha_hora AS FechaHora,
                c.clues AS Clues,
                c.estado AS Estado,
                c.motivo AS Motivo,
                c.notas AS Notas,

                c.usuario_registro_id AS UsuarioRegistroId,
                c.usuario_registro_nombre AS UsuarioRegistroNombre,

                c.fecha_registro AS FechaRegistro,
                c.fecha_actualizacion AS FechaActualizacion

            FROM sncev.cita_vacunacion c

            INNER JOIN sncev.paciente p
                ON p.id = c.paciente_id

            INNER JOIN sncev.vacuna_catalogo vc
                ON vc.id = c.vacuna_id

            WHERE c.id = @Id;
            """;

        var cita =
            await conn.QueryFirstOrDefaultAsync(
                new CommandDefinition(
                    sql,
                    new { Id = id },
                    cancellationToken: cancellationToken));

        if (cita is null)
        {
            return NotFound(new
            {
                message = "No se encontró la cita."
            });
        }

        return Ok(cita);
    }

    // ============================================================
    // POST /v1/citas
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "Administrador,Capturador")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearCitaRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PacienteId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "PacienteId es obligatorio."
            });
        }

        if (request.VacunaId <= 0)
        {
            return BadRequest(new
            {
                message = "VacunaId no es válido."
            });
        }

        if (request.NumeroDosis < 0)
        {
            return BadRequest(new
            {
                message = "NumeroDosis no es válido."
            });
        }

        if (request.FechaHora <= DateTime.Now)
        {
            return BadRequest(new
            {
                message =
                    "La fecha y hora de la cita deben ser posteriores al momento actual."
            });
        }

        // --------------------------------------------------------
        // Identidad desde JWT
        // --------------------------------------------------------

        var usuarioIdTexto =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        Guid? usuarioId =
            Guid.TryParse(usuarioIdTexto, out var uid)
                ? uid
                : null;

        var usuarioNombre =
            User.FindFirstValue("nombre")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("unique_name")
            ?? User.Identity?.Name
            ?? "Usuario no identificado";

        var cluesUsuario =
            User.FindFirstValue("clues");

        if (string.IsNullOrWhiteSpace(cluesUsuario))
            return Forbid();

        // De momento la cita se agenda en la CLUES del usuario.
        // Más adelante Administrador podrá seleccionar otra unidad
        // mediante una regla explícita de autorización.
        var cluesCita = cluesUsuario.Trim();

        // --------------------------------------------------------
        // Validación clínica para LA FECHA DE LA CITA
        // --------------------------------------------------------

        var esquema =
            await _motor.EvaluarAsync(
                request.PacienteId,
                cancellationToken,
                request.FechaHora.Date);

        if (esquema is null)
        {
            return NotFound(new
            {
                message =
                    "No se encontró un paciente activo con ese identificador."
            });
        }

        // En la fecha de la cita la dosis debe ser APLICABLE.
        var dosisAplicable =
            esquema.Aplicables.FirstOrDefault(x =>
                x.VacunaId == request.VacunaId &&
                x.NumeroDosis == request.NumeroDosis);

        if (dosisAplicable is null)
        {
            return Conflict(new
            {
                message =
                    "La vacuna y dosis solicitadas no son aplicables para el paciente en la fecha seleccionada.",

                fechaEvaluacion =
                    request.FechaHora.Date,

                vacunaId =
                    request.VacunaId,

                numeroDosis =
                    request.NumeroDosis,

                aplicablesEnLaFecha =
                    esquema.Aplicables.Select(x => new
                    {
                        x.VacunaId,
                        x.ClaveAnx,
                        x.Vacuna,
                        x.NumeroDosis,
                        x.Estado,
                        x.Motivo
                    })
            });
        }

        await using var conn =
            new SqlConnection(GetConnectionString());

        await conn.OpenAsync(cancellationToken);

        await using var tx =
            (SqlTransaction)
            await conn.BeginTransactionAsync(
                cancellationToken);

        try
        {
            // ----------------------------------------------------
            // Paciente todavía activo
            // ----------------------------------------------------

            const string sqlPaciente = """
                SELECT COUNT(1)
                FROM sncev.paciente WITH (UPDLOCK, HOLDLOCK)
                WHERE id = @PacienteId
                  AND eliminado = 0;
                """;

            var pacienteActivo =
                await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sqlPaciente,
                        new
                        {
                            request.PacienteId
                        },
                        transaction: tx,
                        cancellationToken: cancellationToken));

            if (pacienteActivo == 0)
            {
                await tx.RollbackAsync(cancellationToken);

                return Conflict(new
                {
                    message =
                        "El paciente ya no se encuentra activo."
                });
            }

            // ----------------------------------------------------
            // Evitar dos citas activas para la misma dosis
            // ----------------------------------------------------

            const string sqlDuplicada = """
                SELECT TOP (1)
                    id
                FROM sncev.cita_vacunacion
                    WITH (UPDLOCK, HOLDLOCK)
                WHERE paciente_id = @PacienteId
                  AND vacuna_id = @VacunaId
                  AND numero_dosis = @NumeroDosis
                  AND estado IN ('PROGRAMADA', 'CONFIRMADA');
                """;

            var citaExistente =
                await conn.QueryFirstOrDefaultAsync<Guid?>(
                    new CommandDefinition(
                        sqlDuplicada,
                        new
                        {
                            request.PacienteId,
                            request.VacunaId,
                            request.NumeroDosis
                        },
                        transaction: tx,
                        cancellationToken: cancellationToken));

            if (citaExistente.HasValue)
            {
                await tx.RollbackAsync(cancellationToken);

                return Conflict(new
                {
                    message =
                        "El paciente ya tiene una cita activa para esta vacuna y dosis.",

                    citaId =
                        citaExistente.Value
                });
            }

            // ----------------------------------------------------
            // INSERT
            // ----------------------------------------------------

            var citaId = Guid.NewGuid();

            const string sqlInsert = """
                INSERT INTO sncev.cita_vacunacion
                (
                    id,
                    paciente_id,
                    vacuna_id,
                    numero_dosis,
                    fecha_hora,
                    clues,
                    estado,
                    motivo,
                    notas,
                    usuario_registro_id,
                    usuario_registro_nombre,
                    fecha_registro,
                    fecha_actualizacion
                )
                VALUES
                (
                    @Id,
                    @PacienteId,
                    @VacunaId,
                    @NumeroDosis,
                    @FechaHora,
                    @Clues,
                    'PROGRAMADA',
                    @Motivo,
                    @Notas,
                    @UsuarioRegistroId,
                    @UsuarioRegistroNombre,
                    SYSDATETIME(),
                    SYSDATETIME()
                );
                """;

            await conn.ExecuteAsync(
                new CommandDefinition(
                    sqlInsert,
                    new
                    {
                        Id = citaId,

                        request.PacienteId,
                        request.VacunaId,
                        request.NumeroDosis,
                        request.FechaHora,

                        Clues = cluesCita,

                        Motivo =
                            dosisAplicable.Motivo,

                        Notas =
                            NormalizarNotas(request.Notas),

                        UsuarioRegistroId =
                            usuarioId,

                        UsuarioRegistroNombre =
                            usuarioNombre
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await tx.CommitAsync(cancellationToken);

            return CreatedAtAction(
                nameof(ObtenerPorId),
                new
                {
                    id = citaId
                },
                new
                {
                    id = citaId,

                    pacienteId =
                        request.PacienteId,

                    paciente =
                        esquema.NombrePaciente,

                    vacunaId =
                        dosisAplicable.VacunaId,

                    vacuna =
                        dosisAplicable.Vacuna,

                    numeroDosis =
                        dosisAplicable.NumeroDosis,

                    fechaHora =
                        request.FechaHora,

                    clues =
                        cluesCita,

                    estado =
                        "PROGRAMADA",

                    motivo =
                        dosisAplicable.Motivo
                });
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ============================================================
    // PATCH /v1/citas/{id}/estado
    // ============================================================

    [HttpPatch("{id:guid}/estado")]
    [Authorize(Roles = "Administrador,Capturador")]
    public async Task<IActionResult> CambiarEstado(
        Guid id,
        [FromBody] CambiarEstadoCitaRequest request,
        CancellationToken cancellationToken)
    {
        var estado =
            request.Estado?
                .Trim()
                .ToUpperInvariant();

        var estadosPermitidos = new[]
        {
            "PROGRAMADA",
            "CONFIRMADA",
            "ATENDIDA",
            "NO_ACUDIO",
            "CANCELADA"
        };

        if (string.IsNullOrWhiteSpace(estado) ||
            !estadosPermitidos.Contains(estado))
        {
            return BadRequest(new
            {
                message =
                    "Estado de cita no válido."
            });
        }

        var cluesUsuario =
            User.FindFirstValue("clues");

        if (string.IsNullOrWhiteSpace(cluesUsuario))
            return Forbid();

        await using var conn =
            new SqlConnection(GetConnectionString());

        await conn.OpenAsync(cancellationToken);

        // Capturador sólo puede modificar citas de su CLUES.
        const string sql = """
            UPDATE sncev.cita_vacunacion
            SET
                estado = @Estado,
                notas =
                    CASE
                        WHEN @Notas IS NULL
                            THEN notas
                        ELSE @Notas
                    END,
                fecha_actualizacion = SYSDATETIME()
            WHERE id = @Id

              AND
              (
                  @EsAdministrador = 1
                  OR clues = @CluesUsuario
              );
            """;

        var afectados =
            await conn.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Id = id,
                        Estado = estado,

                        Notas =
                            NormalizarNotas(request.Notas),

                        EsAdministrador =
                            User.IsInRole("Administrador"),

                        CluesUsuario =
                            cluesUsuario.Trim()
                    },
                    cancellationToken: cancellationToken));

        if (afectados == 0)
        {
            return NotFound(new
            {
                message =
                    "No se encontró la cita o no pertenece a la unidad autorizada."
            });
        }

        return Ok(new
        {
            id,
            estado
        });
    }

    // ============================================================
    // Helpers
    // ============================================================

    private string GetConnectionString()
    {
        return _config
            .GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:SqlServerConnection no está configurada.");
    }

    private static string? NormalizarNotas(
        string? notas)
    {
        if (string.IsNullOrWhiteSpace(notas))
            return null;

        var valor = notas.Trim();

        return valor.Length <= 1000
            ? valor
            : valor[..1000];
    }

    // ============================================================
    // Requests
    // ============================================================

    public sealed class CrearCitaRequest
    {
        public Guid PacienteId { get; set; }

        public int VacunaId { get; set; }

        public int NumeroDosis { get; set; }

        public DateTime FechaHora { get; set; }

        public string? Notas { get; set; }
    }

    public sealed class CambiarEstadoCitaRequest
    {
        public string Estado { get; set; } =
            string.Empty;

        public string? Notas { get; set; }
    }
}