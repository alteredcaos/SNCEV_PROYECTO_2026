using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SNCEV.Api.Models;
using SNCEV.Api.Services;
using System.Security.Claims;
using System.Text.Json;
using System.Transactions;
namespace SNCEV.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/pacientes/{pacienteId:guid}/vacunaciones")]
public sealed class VacunacionesController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly MotorEsquemaVacunacionService _motor;

    public VacunacionesController(
        IConfiguration config,
        MotorEsquemaVacunacionService motor)
    {
        _config = config;
        _motor = motor;
    }
    [HttpPost("{aplicacionId:guid}/anular")]
    [Authorize(Roles = "Administrador,Capturador")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Anular(
    Guid pacienteId,
    Guid aplicacionId,
    [FromBody] AnularVacunacionRequest request,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return BadRequest(new
            {
                message = "Debe especificar el motivo de la anulación."
            });
        }

        var motivo = request.Motivo.Trim();

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

        var usuarioRol =
            User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? "Rol no identificado";

        var clues =
            User.FindFirstValue("clues");

        if (string.IsNullOrWhiteSpace(clues))
            return Forbid();

        var ipOrigen =
            HttpContext.Connection.RemoteIpAddress?.ToString();
        var connectionString =
            _config.GetConnectionString("SqlServerConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "No está configurada la conexión a SQL Server."
                });
        }

        await using var conn =
            new SqlConnection(connectionString);

        await conn.OpenAsync(cancellationToken);

        await using var tx =
            await conn.BeginTransactionAsync(cancellationToken);

        try
        {
            // Bloqueamos el registro mientras se realiza la anulación.
            const string sqlAplicacion = """
            SELECT
                id AS Id,
                paciente_id AS PacienteId,
                vacuna_id AS VacunaId,
                numero_dosis AS NumeroDosis,
                fecha_aplicacion AS FechaAplicacion,
                lote AS Lote,
                laboratorio AS Laboratorio,
                unidad_medica_clues AS UnidadMedicaClues,
                observaciones AS Observaciones,
                grupo_poblacional AS GrupoPoblacional,
                anulada AS Anulada
            FROM sncev.vacuna_aplicada WITH (UPDLOCK, HOLDLOCK)
            WHERE id = @AplicacionId
              AND paciente_id = @PacienteId;
            """;

            var aplicacion =
                await conn.QueryFirstOrDefaultAsync<dynamic>(
                    new CommandDefinition(
                        sqlAplicacion,
                        new
                        {
                            AplicacionId = aplicacionId,
                            PacienteId = pacienteId
                        },
                        transaction: tx,
                        cancellationToken: cancellationToken));

            if (aplicacion is null)
            {
                await tx.RollbackAsync(cancellationToken);

                return NotFound(new
                {
                    message = "No se encontró la aplicación de vacuna para este paciente."
                });
            }

            if ((bool)aplicacion.Anulada)
            {
                await tx.RollbackAsync(cancellationToken);

                return Conflict(new
                {
                    message = "La aplicación ya se encuentra anulada."
                });
            }

            var datosAnteriores = JsonSerializer.Serialize(new
            {
                aplicacionId,
                pacienteId,
                vacunaId = (int)aplicacion.VacunaId,
                numeroDosis = (byte)aplicacion.NumeroDosis,
                fechaAplicacion = aplicacion.FechaAplicacion,
                lote = (string?)aplicacion.Lote,
                laboratorio = (string?)aplicacion.Laboratorio,
                unidadMedicaClues = (string?)aplicacion.UnidadMedicaClues,
                observaciones = (string?)aplicacion.Observaciones,
                grupoPoblacional = (string?)aplicacion.GrupoPoblacional,
                anulada = false
            });

            const string sqlAnular = """
            UPDATE sncev.vacuna_aplicada
            SET
                anulada = 1,
                fecha_anulacion = SYSDATETIME(),
                motivo_anulacion = @Motivo
            WHERE id = @AplicacionId
              AND paciente_id = @PacienteId
              AND anulada = 0;
            """;

            var filas =
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlAnular,
                        new
                        {
                            AplicacionId = aplicacionId,
                            PacienteId = pacienteId,
                            Motivo = motivo
                        },
                        transaction: tx,
                        cancellationToken: cancellationToken));

            if (filas != 1)
            {
                await tx.RollbackAsync(cancellationToken);

                return Conflict(new
                {
                    message = "La aplicación no pudo ser anulada porque su estado cambió."
                });
            }

            var datosNuevos = JsonSerializer.Serialize(new
            {
                aplicacionId,
                anulada = true,
                motivoAnulacion = motivo
            });

            const string sqlAuditoria = """
            INSERT INTO sncev.auditoria_vacunacion
            (
                aplicacion_id,
                paciente_id,
                accion,
                vacuna_id,
                numero_dosis,
                motivo,
                usuario_id,
                usuario_nombre,
                usuario_rol,
                unidad_medica_clues,
                fecha,
                datos_anteriores,
                datos_nuevos,
                ip_origen
            )
            VALUES
            (
                @AplicacionId,
                @PacienteId,
                'ANULACION',
                @VacunaId,
                @NumeroDosis,
                @Motivo,
                @UsuarioId,
                @UsuarioNombre,
                @UsuarioRol,
                @Clues,
                SYSDATETIME(),
                @DatosAnteriores,
                @DatosNuevos,
                @IpOrigen
            );
            """;

            await conn.ExecuteAsync(
                new CommandDefinition(
                    sqlAuditoria,
                    new
                    {
                        AplicacionId = aplicacionId,
                        PacienteId = pacienteId,
                        VacunaId = (int)aplicacion.VacunaId,
                        NumeroDosis = (byte)aplicacion.NumeroDosis,
                        Motivo = motivo,
                        UsuarioId = usuarioId,
                        UsuarioNombre = usuarioNombre,
                        UsuarioRol = usuarioRol,
                        Clues = clues.Trim(),
                        DatosAnteriores = datosAnteriores,
                        DatosNuevos = datosNuevos,
                        IpOrigen = ipOrigen
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await tx.CommitAsync(cancellationToken);

            return Ok(new
            {
                message = "La aplicación de vacuna fue anulada correctamente.",
                aplicacionId,
                pacienteId,
                motivo
            });
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
    [HttpPost]
    [Authorize(Roles = "Administrador,Capturador")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(
        Guid pacienteId,
        [FromBody] RegistrarVacunacionRequest request,
        CancellationToken cancellationToken)
    {
        // -------------------------------------------------
        // 1. Validaciones básicas
        // -------------------------------------------------

        if (request.FechaAplicacion.Date > DateTime.Today)
        {
            return BadRequest(new
            {
                message =
                    "La fecha de aplicación no puede ser futura."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Lote))
        {
            return BadRequest(new
            {
                message = "El lote es obligatorio."
            });
        }

        // -------------------------------------------------
        // 2. Evaluar esquema en servidor
        // -------------------------------------------------

        var esquema =
            await _motor.EvaluarAsync(
                pacienteId,
                cancellationToken);

        if (esquema is null)
        {
            return NotFound(new
            {
                message =
                    "No se encontró un paciente activo con ese identificador."
            });
        }

        // -------------------------------------------------
        // 3. La vacuna/dosis DEBE estar APLICABLE.
        //
        // No confiamos en lo que mande React.
        // -------------------------------------------------

        var dosisAplicable =
            esquema.Aplicables.FirstOrDefault(x =>
                x.VacunaId == request.VacunaId &&
                x.NumeroDosis == request.NumeroDosis);

        if (dosisAplicable is null)
        {
            return Conflict(new
            {
                message =
                    "La vacuna y dosis solicitadas no están actualmente marcadas como APLICABLE para este paciente.",

                vacunaId = request.VacunaId,
                numeroDosis = request.NumeroDosis,

                aplicablesActualmente =
                    esquema.Aplicables.Select(x => new
                    {
                        x.VacunaId,
                        x.ClaveAnx,
                        x.Vacuna,
                        x.NumeroDosis,
                        x.Estado
                    })
            });
        }

        // -------------------------------------------------
        // 4. Identidad obtenida del JWT
        // -------------------------------------------------

        var usuarioIdTexto =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        Guid? usuarioId = null;

        if (Guid.TryParse(usuarioIdTexto, out var uid))
            usuarioId = uid;

        var username =
            User.Identity?.Name
            ?? User.FindFirstValue("unique_name")
            ?? User.FindFirstValue("name")
            ?? "usuario";

        var clues =
            User.FindFirstValue("clues");

        if (string.IsNullOrWhiteSpace(clues))
        {
            return Forbid();
        }

        // -------------------------------------------------
        // 5. Conexión
        // -------------------------------------------------

        var connectionString =
            _config.GetConnectionString("SqlServerConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "No está configurada la conexión a SQL Server."
                });
        }

        await using var conn =
            new SqlConnection(connectionString);

        await conn.OpenAsync(cancellationToken);

        await using var tx =
            (SqlTransaction)await conn.BeginTransactionAsync(
                cancellationToken);

        try
        {
            // ---------------------------------------------
            // 6. Volver a comprobar paciente DENTRO
            //    de la transacción.
            // ---------------------------------------------

            const string sqlPaciente = """
                SELECT COUNT(1)
                FROM sncev.paciente WITH (UPDLOCK, HOLDLOCK)
                WHERE id = @PacienteId
                  AND eliminado = 0;
                """;

            var existePaciente =
                await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sqlPaciente,
                        new
                        {
                            PacienteId = pacienteId
                        },
                        transaction: tx,
                        cancellationToken:
                            cancellationToken));

            if (existePaciente == 0)
            {
                await tx.RollbackAsync(cancellationToken);

                return Conflict(new
                {
                    message =
                        "El paciente ya no se encuentra activo."
                });
            }

            // ---------------------------------------------
            // 7. Comprobar vacuna activa.
            // ---------------------------------------------

            const string sqlVacuna = """
                SELECT
                    id,
                    clave_anx AS ClaveAnx,
                    nombre_generico AS Nombre
                FROM sncev.vacuna_catalogo
                WHERE id = @VacunaId
                  AND activo = 1;
                """;

            var vacuna =
                await conn.QuerySingleOrDefaultAsync<VacunaDb>(
                    new CommandDefinition(
                        sqlVacuna,
                        new
                        {
                            VacunaId = request.VacunaId
                        },
                        transaction: tx,
                        cancellationToken:
                            cancellationToken));

            if (vacuna is null)
            {
                await tx.RollbackAsync(cancellationToken);

                return Conflict(new
                {
                    message =
                        "La vacuna no existe o se encuentra inactiva."
                });
            }

            // ---------------------------------------------
            // 8. Evitar registrar dos veces
            //    vacuna + dosis para el mismo paciente.
            // ---------------------------------------------

            const string sqlDuplicado = """
                SELECT COUNT(1)
                FROM sncev.vacuna_aplicada WITH (UPDLOCK, HOLDLOCK)
                WHERE paciente_id = @PacienteId
                  AND vacuna_id = @VacunaId
                  AND numero_dosis = @NumeroDosis
                  AND anulada = 0;
                """;

            var duplicado =
                await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sqlDuplicado,
                        new
                        {
                            PacienteId = pacienteId,
                            VacunaId = request.VacunaId,
                            NumeroDosis = request.NumeroDosis
                        },
                        transaction: tx,
                        cancellationToken:
                            cancellationToken));

            if (duplicado > 0)
            {
                await tx.RollbackAsync(cancellationToken);

                return Conflict(new
                {
                    message =
                        "Esta vacuna y número de dosis ya fueron registrados para el paciente."
                });
            }

            // ---------------------------------------------
            // 9. Insert
            // ---------------------------------------------

            var aplicacionId = Guid.NewGuid();

            const string sqlInsert = """
    INSERT INTO sncev.vacuna_aplicada
    (
        id,
        paciente_id,
        vacuna_id,
        numero_dosis,
        fecha_aplicacion,
        lote,
        laboratorio,
        unidad_medica_clues,
        aplicador_curp,
        aplicador_usuario_id,
        es_esquema_completo,
        observaciones,
        firma_digital_kms,
        sincronizado,
        fecha_registro,
        grupo_poblacional
    )
    VALUES
    (
        @Id,
        @PacienteId,
        @VacunaId,
        @NumeroDosis,
        @FechaAplicacion,
        @Lote,
        @Laboratorio,
        @Clues,
        NULL,
        @AplicadorUsuarioId,
        0,
        @Observaciones,
        NULL,
        1,
        SYSDATETIME(),
        @GrupoPoblacional
    );
    """;

            await conn.ExecuteAsync(
                new CommandDefinition(
                    sqlInsert,
                    new
                    {
                        Id = aplicacionId,
                        PacienteId = pacienteId,
                        VacunaId = request.VacunaId,
                        NumeroDosis = request.NumeroDosis,
                        FechaAplicacion = request.FechaAplicacion.Date,

                        Lote = request.Lote.Trim(),

                        Laboratorio =
                            Limpiar(request.Laboratorio),

                        Clues = clues.Trim(),
                        AplicadorUsuarioId = usuarioId,

                        Observaciones =
                            Limpiar(request.Observaciones),

                        GrupoPoblacional =
                            Limpiar(request.GrupoPoblacional)
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));


            // ==========================================================
            // AUDITORÍA DE LA APLICACIÓN
            // ==========================================================

            var datosNuevos = JsonSerializer.Serialize(new
            {
                aplicacionId,
                pacienteId,
                vacunaId = request.VacunaId,
                numeroDosis = request.NumeroDosis,
                fechaAplicacion = request.FechaAplicacion.Date,
                lote = request.Lote.Trim(),
                laboratorio = Limpiar(request.Laboratorio),
                unidadMedicaClues = clues.Trim(),
                observaciones = Limpiar(request.Observaciones),
                grupoPoblacional = Limpiar(request.GrupoPoblacional)
            });

            var usuarioNombre =
                User.FindFirstValue("nombre")
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("unique_name")
                ?? User.Identity?.Name
                ?? "Usuario no identificado";

            var usuarioRol =
                User.FindFirstValue(ClaimTypes.Role)
                ?? User.FindFirstValue("role")
                ?? "Rol no identificado";

            var ipOrigen =
                HttpContext.Connection.RemoteIpAddress?.ToString();

            const string sqlAuditoria = """
    INSERT INTO sncev.auditoria_vacunacion
    (
        aplicacion_id,
        paciente_id,
        accion,
        vacuna_id,
        numero_dosis,
        motivo,
        usuario_id,
        usuario_nombre,
        usuario_rol,
        unidad_medica_clues,
        fecha,
        datos_anteriores,
        datos_nuevos,
        ip_origen
    )
    VALUES
    (
        @AplicacionId,
        @PacienteId,
        'APLICACION',
        @VacunaId,
        @NumeroDosis,
        @Motivo,
        @UsuarioId,
        @UsuarioNombre,
        @UsuarioRol,
        @Clues,
        SYSDATETIME(),
        NULL,
        @DatosNuevos,
        @IpOrigen
    );
    """;

            await conn.ExecuteAsync(
                new CommandDefinition(
                    sqlAuditoria,
                    new
                    {
                        AplicacionId = aplicacionId,
                        PacienteId = pacienteId,
                        VacunaId = request.VacunaId,
                        NumeroDosis = request.NumeroDosis,

                        Motivo = "Registro de aplicación de vacuna",

                        UsuarioId = usuarioId,
                        UsuarioNombre = usuarioNombre,
                        UsuarioRol = usuarioRol,
                        Clues = clues.Trim(),

                        DatosNuevos = datosNuevos,
                        IpOrigen = ipOrigen
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            // ==========================================================
            // CERRAR AUTOMÁTICAMENTE LA CITA ACTIVA RELACIONADA
            // ==========================================================
            const string sqlCerrarCita = """
                ;WITH CitaActiva AS
                (
                    SELECT TOP (1) id
                    FROM sncev.cita_vacunacion WITH (UPDLOCK, HOLDLOCK)
                    WHERE paciente_id = @PacienteId
                      AND vacuna_id = @VacunaId
                      AND numero_dosis = @NumeroDosis
                      AND estado IN ('PROGRAMADA', 'CONFIRMADA')
                    ORDER BY
                        CASE WHEN estado = 'CONFIRMADA' THEN 0 ELSE 1 END,
                        ABS(DATEDIFF(MINUTE, fecha_hora, @FechaAplicacion)),
                        fecha_hora
                )
                UPDATE c
                SET
                    c.estado = 'ATENDIDA',
                    c.fecha_actualizacion = SYSDATETIME()
                OUTPUT inserted.id
                FROM sncev.cita_vacunacion c
                INNER JOIN CitaActiva ca ON ca.id = c.id;
                """;

            var citaAtendidaId =
                await conn.QueryFirstOrDefaultAsync<Guid?>(
                    new CommandDefinition(
                        sqlCerrarCita,
                        new
                        {
                            PacienteId = pacienteId,
                            VacunaId = request.VacunaId,
                            NumeroDosis = request.NumeroDosis,
                            FechaAplicacion = request.FechaAplicacion.Date
                        },
                        transaction: tx,
                        cancellationToken: cancellationToken));

            // ==========================================================
            // CONFIRMAR APLICACIÓN + AUDITORÍA + CIERRE DE CITA
            // EN UNA SOLA TRANSACCIÓN
            // ==========================================================
            await tx.CommitAsync(cancellationToken);

            // ---------------------------------------------
            // 10. Respuesta
            // ---------------------------------------------

            return Created(
                $"/v1/pacientes/{pacienteId}/vacunaciones/{aplicacionId}",
                new
                {
                    id = aplicacionId,
                    pacienteId,
                    vacunaId = vacuna.Id,
                    claveAnx = vacuna.ClaveAnx,
                    vacuna = vacuna.Nombre,
                    numeroDosis = request.NumeroDosis,
                    fechaAplicacion =
                        request.FechaAplicacion.Date,
                    lote = request.Lote.Trim(),
                    laboratorio =
                        Limpiar(request.Laboratorio),
                    unidadMedicaClues = clues.Trim(),
                    usuarioId,
                    usuario = username,
                    citaAtendida = citaAtendidaId.HasValue,
                    citaAtendidaId
                });
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(cancellationToken);
            }
            catch
            {
                // No ocultar la excepción original.
            }

            throw;
        }
    }

    private static string? Limpiar(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Trim();
    }

    private sealed class VacunaDb
    {
        public int Id { get; set; }

        public string ClaveAnx { get; set; } =
            string.Empty;

        public string Nombre { get; set; } =
            string.Empty;
    }
    private sealed class AuditoriaVacunacionDb
    {
        public long Id { get; set; }

        public Guid AplicacionId { get; set; }

        public Guid PacienteId { get; set; }

        public string Accion { get; set; } = string.Empty;

        public int VacunaId { get; set; }

        public string? ClaveAnx { get; set; }

        public string? Vacuna { get; set; }

        public byte NumeroDosis { get; set; }

        public string? Motivo { get; set; }

        public Guid? UsuarioId { get; set; }

        public string? UsuarioNombre { get; set; }

        public string? UsuarioRol { get; set; }

        public string? UnidadMedicaClues { get; set; }

        public DateTime Fecha { get; set; }

        public string? DatosAnteriores { get; set; }

        public string? DatosNuevos { get; set; }

        public string? IpOrigen { get; set; }

        public bool? AplicacionAnulada { get; set; }

        public DateTime? FechaAnulacion { get; set; }

        public string? MotivoAnulacion { get; set; }
    }

    // ==========================================================
    // AUDITORÍA DE VACUNACIÓN DEL PACIENTE
    // ==========================================================
    [HttpGet("auditoria")]
    [Authorize(Roles = "Administrador,Capturador,Directivo,Auditor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAuditoria(
        Guid pacienteId,
        CancellationToken cancellationToken)
    {
        var connectionString =
            _config.GetConnectionString("SqlServerConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "No está configurada la conexión a SQL Server."
                });
        }

        await using var conn =
            new SqlConnection(connectionString);

        await conn.OpenAsync(cancellationToken);

        // ------------------------------------------------------
        // 1. Comprobar que el paciente exista.
        //    No exigimos eliminado = 0 porque la auditoría
        //    también debe poder consultarse históricamente.
        // ------------------------------------------------------
        const string sqlPaciente = """
        SELECT COUNT(1)
        FROM sncev.paciente
        WHERE id = @PacienteId;
        """;

        var existePaciente =
            await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    sqlPaciente,
                    new
                    {
                        PacienteId = pacienteId
                    },
                    cancellationToken: cancellationToken));

        if (existePaciente == 0)
        {
            return NotFound(new
            {
                message = "No se encontró el paciente."
            });
        }

        // ------------------------------------------------------
        // 2. Recuperar TODOS los eventos de auditoría.
        //
        // Incluye:
        // - APLICACION
        // - ANULACION
        //
        // No filtramos vacuna_aplicada.anulada porque precisamente
        // queremos conservar y mostrar el historial completo.
        // ------------------------------------------------------
        const string sqlAuditoria = """
        SELECT
            a.id AS Id,
            a.aplicacion_id AS AplicacionId,
            a.paciente_id AS PacienteId,
            a.accion AS Accion,

            a.vacuna_id AS VacunaId,
            vc.clave_anx AS ClaveAnx,
            vc.nombre_generico AS Vacuna,

            a.numero_dosis AS NumeroDosis,
            a.motivo AS Motivo,

            a.usuario_id AS UsuarioId,
            a.usuario_nombre AS UsuarioNombre,
            a.usuario_rol AS UsuarioRol,

            a.unidad_medica_clues AS UnidadMedicaClues,
            a.fecha AS Fecha,

            a.datos_anteriores AS DatosAnteriores,
            a.datos_nuevos AS DatosNuevos,
            a.ip_origen AS IpOrigen,

            va.anulada AS AplicacionAnulada,
            va.fecha_anulacion AS FechaAnulacion,
            va.motivo_anulacion AS MotivoAnulacion

        FROM sncev.auditoria_vacunacion a

        LEFT JOIN sncev.vacuna_catalogo vc
            ON vc.id = a.vacuna_id

        LEFT JOIN sncev.vacuna_aplicada va
            ON va.id = a.aplicacion_id

        WHERE a.paciente_id = @PacienteId

        ORDER BY
            a.fecha DESC,
            a.id DESC;
        """;

        var eventos =
            (await conn.QueryAsync<AuditoriaVacunacionDb>(
                new CommandDefinition(
                    sqlAuditoria,
                    new
                    {
                        PacienteId = pacienteId
                    },
                    cancellationToken: cancellationToken)))
            .ToList();

        // ------------------------------------------------------
        // 3. Respuesta
        // ------------------------------------------------------
        return Ok(new
        {
            pacienteId,
            total = eventos.Count,
            eventos
        });
    }

}