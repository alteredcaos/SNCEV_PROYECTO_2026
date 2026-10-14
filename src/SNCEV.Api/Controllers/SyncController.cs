// =============================================================================
// SNCEV API — SyncController
// Endpoints Delta Sync (Push/Pull) para Aplicación Móvil Offline
// =============================================================================

using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

namespace SNCEV.Api.Controllers;

[ApiController]
[Route("v1/sync")]
public class SyncController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<SyncController> _logger;

    public SyncController(IConfiguration config, ILogger<SyncController> logger)
    {
        _config = config;
        _logger = logger;
    }

    public class PushPayload
    {
        public List<dynamic> Pacientes { get; set; } = new();
        public List<dynamic> VacunasAplicadas { get; set; } = new();
        public List<dynamic> Somatometria { get; set; } = new();
    }

    [HttpPost("push")]
    public async Task<IActionResult> SyncPush([FromBody] PushPayload payload)
    {
        _logger.LogInformation("Sync Push recibido: {Pacientes} pacientes, {Vacunas} vacunas",
            payload.Pacientes.Count, payload.VacunasAplicadas.Count);

        using var conn = new SqlConnection(_config.GetConnectionString("SqlServerConnection"));
        await conn.OpenAsync();
        using var tran = conn.BeginTransaction();

        try
        {
            int aplicadasInsertadas = 0;

            foreach (var v in payload.VacunasAplicadas)
            {
                string sql = @"
                    IF NOT EXISTS (SELECT 1 FROM sncev.vacuna_aplicada WHERE id = @id)
                    BEGIN
                        INSERT INTO sncev.vacuna_aplicada
                        (id, paciente_id, vacuna_id, numero_dosis, fecha_aplicacion, lote, laboratorio, unidad_medica_clues, aplicador_curp, observaciones, fecha_registro)
                        VALUES (@id, @paciente_id, @vacuna_id, @numero_dosis, @fecha_aplicacion, @lote, @laboratorio, @unidad_medica_clues, @aplicador_curp, @observaciones, SYSDATETIME());
                    END";

                await conn.ExecuteAsync(sql, (object)v, transaction: tran);
                aplicadasInsertadas++;
            }

            tran.Commit();
            return Ok(new { exitoso = true, procesados = aplicadasInsertadas, timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            tran.Rollback();
            _logger.LogError(ex, "Error durante Sync Push");
            return StatusCode(500, new { error = "Error procesando sincronización", detalle = ex.Message });
        }
    }

    [HttpGet("pull")]
    public async Task<IActionResult> SyncPull([FromQuery] DateTime? desde)
    {
        DateTime fechaFiltro = desde ?? DateTime.UtcNow.AddDays(-30);
        using var conn = new SqlConnection(_config.GetConnectionString("SqlServerConnection"));

        var vacunasNuevas = await conn.QueryAsync(
            "SELECT * FROM sncev.vacuna_aplicada WHERE fecha_registro >= @Desde",
            new { Desde = fechaFiltro }
        );

        var pacientesNuevos = await conn.QueryAsync(
            "SELECT id, curp, nombre, primer_apellido, segundo_apellido, fecha_nacimiento, sexo, municipio_residencia_id FROM sncev.paciente WHERE fecha_actualizacion >= @Desde",
            new { Desde = fechaFiltro }
        );

        return Ok(new
        {
            timestampServidor = DateTime.UtcNow,
            pacientes = pacientesNuevos,
            vacunasAplicadas = vacunasNuevas
        });
    }
}
