using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SNCEV.Api.Controllers;

// Versión de integración para desarrollo. Requiere la migración 001_sncev_sync.sql.
// No expone un pull nominal sin políticas de ámbito.
[ApiController]
[Authorize]
[Route("v1/sync/v2")]
public sealed class SyncV2Controller : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncV2Controller> _logger;
    public SyncV2Controller(IConfiguration configuration, ILogger<SyncV2Controller> logger)
    { _configuration = configuration; _logger = logger; }

    public sealed record PushRequest(Guid DispositivoId, List<OperacionDto> Operaciones);
    public sealed record OperacionDto(Guid OperacionId, string Entidad, Guid EntidadLocalId, JsonElement Payload);
    public sealed record ResultadoDto(Guid OperacionId, string Estado, Guid? EntidadCentralId, string? Codigo);
    public sealed record PushResponse(List<ResultadoDto> Resultados);
    public sealed record PacienteDto(Guid Id, string? Curp, string Nombre, string PrimerApellido,
        string? SegundoApellido, DateTime FechaNacimiento, string Sexo, string EntidadNacimientoClave,
        int MunicipioResidenciaId);
    public sealed record DosisDto(Guid Id, Guid PacienteId, int VacunaId, int NumeroDosis,
        DateTime FechaAplicacion, string Lote, string? Laboratorio, string UnidadMedicaClues,
        string? Observaciones);

    [HttpPost("push")]
    public async Task<ActionResult<PushResponse>> Push([FromBody] PushRequest request, CancellationToken ct)
    {
        // Claim names must be checked against the actual AuthController token generator.
        string? subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        string? clues = User.FindFirstValue("clues") ?? User.FindFirstValue("clues_asignada");
        if (!Guid.TryParse(subject, out var userId) || string.IsNullOrWhiteSpace(clues))
            return Forbid();
        if (request.DispositivoId == Guid.Empty || request.Operaciones is null || request.Operaciones.Count is < 1 or > 50)
            return BadRequest(new { error = "Lote inválido (1 a 50 operaciones)." });
        if (request.Operaciones.Any(x => x.OperacionId == Guid.Empty || x.EntidadLocalId == Guid.Empty)
            || request.Operaciones.Select(x => x.OperacionId).Distinct().Count() != request.Operaciones.Count)
            return BadRequest(new { error = "UUID de operación inválido o repetido." });

        var results = new List<ResultadoDto>();
        await using var conn = new SqlConnection(_configuration.GetConnectionString("SqlServerConnection"));
        await conn.OpenAsync(ct);
        // Process patients first, preserving result order by operation UUID.
        foreach (var op in request.Operaciones.OrderBy(x => x.Entidad == "Paciente" ? 0 : 1))
        {
            try { results.Add(await ProcessOne(conn, request.DispositivoId, userId, clues, op, ct)); }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error SQL al procesar operación {OperationId}", op.OperacionId);
                // The mobile must retain the operation and retry. Never claim confirmation.
                return StatusCode(503, new { error = "Sincronización temporalmente no disponible." });
            }
        }
        return Ok(new PushResponse(results));
    }

    private async Task<ResultadoDto> ProcessOne(SqlConnection conn, Guid device, Guid userId,
        string clues, OperacionDto op, CancellationToken ct)
    {
        // Per-operation serializable transaction guarantees durable idempotency and clinical write atomicity.
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
                op.Entidad, op.EntidadLocalId, op.Payload
            })));
            var old = await conn.QuerySingleOrDefaultAsync<StoredResult>(new CommandDefinition(@"
SELECT estado AS Estado, entidad_central_id AS EntidadCentralId, codigo_error AS Codigo,
       payload_sha256 AS Hash, dispositivo_id AS DispositivoId, usuario_id AS UsuarioId
FROM sncev_sync.operacion WITH (UPDLOCK, HOLDLOCK) WHERE operacion_id=@Id",
                new { Id = op.OperacionId }, tx, cancellationToken: ct));
            if (old != null)
            {
                tx.Commit();
                if (!hash.SequenceEqual(old.Hash) || old.DispositivoId != device || old.UsuarioId != userId)
                    return new(op.OperacionId, "Rechazada", null, "OPERACION_ID_REUTILIZADO");
                return new(op.OperacionId, old.Estado, old.EntidadCentralId, old.Codigo);
            }
            if (op.Entidad != "Paciente" && op.Entidad != "VacunaAplicada")
                return RollbackResult(tx, op, "Rechazada", "ENTIDAD_INVALIDA");
            await conn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO sncev_sync.operacion
(operacion_id,dispositivo_id,entidad,entidad_local_id,usuario_id,clues,payload_sha256,estado)
VALUES (@Op,@Device,@Entity,@Local,@UserId,@Clues,@Hash,'Recibida')",
                new { Op=op.OperacionId, Device=device, Entity=op.Entidad, Local=op.EntidadLocalId,
                    UserId=userId, Clues=clues, Hash=hash }, tx, cancellationToken: ct));

            string state = "Conflicto";
            string? code = null;
            Guid? central = null;
            if (op.Entidad == "Paciente")
            {
                var p = op.Payload.Deserialize<PacienteDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (p == null || p.Id != op.EntidadLocalId || string.IsNullOrWhiteSpace(p.Nombre) ||
                    string.IsNullOrWhiteSpace(p.PrimerApellido) || p.FechaNacimiento.Date > DateTime.UtcNow.Date ||
                    p.MunicipioResidenciaId <= 0 || p.EntidadNacimientoClave?.Length != 2 ||
                    (p.Sexo != "M" && p.Sexo != "F"))
                    (state, code) = ("Rechazada", "PACIENTE_INVALIDO");
                else if (string.IsNullOrWhiteSpace(p.Curp) || p.Curp.Trim().Length != 18)
                    code = "IDENTIDAD_REQUIERE_REVISION";
                else
                {
                    string curp = p.Curp.Trim().ToUpperInvariant();
                    var existing = await conn.QueryAsync<IdentityCandidate>(new CommandDefinition(@"
SELECT id AS Id, nombre AS Nombre, primer_apellido AS PrimerApellido,
       fecha_nacimiento AS FechaNacimiento, sexo AS Sexo
FROM sncev.paciente WITH (UPDLOCK, HOLDLOCK) WHERE curp=@Curp AND eliminado=0",
                        new { Curp=curp }, tx, cancellationToken: ct));
                    var candidates = existing.ToList();
                    if (candidates.Count == 1 && SameIdentity(candidates[0], p))
                    { central = candidates[0].Id; state = "Confirmada"; }
                    else if (candidates.Count > 0) code = "IDENTIDAD_DISCREPANTE";
                    else
                    {
                        // Conservative: no automatic enrollment until explicit clinical/identity approval.
                        code = "ALTA_PACIENTE_REQUIERE_VALIDACION";
                    }
                }
                if (state == "Confirmada" && central.HasValue)
                    await conn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO sncev_sync.paciente_equivalencia
(dispositivo_id,paciente_local_id,paciente_central_id,operacion_id)
VALUES (@Device,@Local,@Central,@Op)",
                        new { Device=device, Local=op.EntidadLocalId, Central=central, Op=op.OperacionId },
                        tx, cancellationToken: ct));
            }
            else
            {
                var d = op.Payload.Deserialize<DosisDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (d == null || d.Id != op.EntidadLocalId || d.PacienteId == Guid.Empty ||
                    d.VacunaId <= 0 || d.NumeroDosis <= 0 || d.NumeroDosis > 255 ||
                    string.IsNullOrWhiteSpace(d.Lote) || d.UnidadMedicaClues != clues ||
                    d.FechaAplicacion.Date > DateTime.UtcNow.Date)
                    (state, code) = ("Rechazada", "DOSIS_INVALIDA");
                else
                {
                    var patientId = await conn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(@"
SELECT paciente_central_id FROM sncev_sync.paciente_equivalencia WITH (UPDLOCK, HOLDLOCK)
WHERE dispositivo_id=@Device AND paciente_local_id=@Local",
                        new { Device=device, Local=d.PacienteId }, tx, cancellationToken: ct));
                    code = patientId.HasValue ? "VALIDACION_CLINICA_PENDIENTE" : "PACIENTE_NO_CONCILIADO";
                    // Do not insert a clinical dose until server-side eligibility, lot, duplicate and
                    // schedule checks are integrated with the existing vaccination service.
                }
            }
            await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE sncev_sync.operacion SET estado=@State, entidad_central_id=@Central,
 codigo_error=@Code, fecha_resolucion=SYSUTCDATETIME() WHERE operacion_id=@Op",
                new { State=state, Central=central, Code=code, Op=op.OperacionId }, tx, cancellationToken: ct));
            if (state == "Conflicto")
                await conn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO sncev_sync.conflicto(operacion_id,tipo,descripcion)
VALUES (@Op,@Type,@Description)", new { Op=op.OperacionId,
                    Type=op.Entidad == "Paciente" ? "Identidad" : "ValidacionClinica",
                    Description=code ?? "Revisión requerida" }, tx, cancellationToken: ct));
            tx.Commit();
            return new(op.OperacionId, state, central, code);
        }
        catch { tx.Rollback(); throw; }
    }

    private static ResultadoDto RollbackResult(SqlTransaction tx, OperacionDto op, string state, string code)
    { tx.Rollback(); return new(op.OperacionId, state, null, code); }
    private static bool SameIdentity(IdentityCandidate a, PacienteDto b) =>
        a.FechaNacimiento.Date == b.FechaNacimiento.Date && a.Sexo == b.Sexo &&
        string.Equals(a.Nombre.Trim(), b.Nombre.Trim(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.PrimerApellido.Trim(), b.PrimerApellido.Trim(), StringComparison.OrdinalIgnoreCase);
    private sealed class StoredResult
    {
        public string Estado { get; set; } = "";
        public Guid? EntidadCentralId { get; set; }
        public string? Codigo { get; set; }
        public byte[] Hash { get; set; } = Array.Empty<byte>();
        public Guid DispositivoId { get; set; }
        public Guid UsuarioId { get; set; }
    }
    private sealed class IdentityCandidate
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = "";
        public string PrimerApellido { get; set; } = "";
        public DateTime FechaNacimiento { get; set; }
        public string Sexo { get; set; } = "";
    }

    [HttpGet("pull")]
    public IActionResult Pull() => StatusCode(501, new { error = "Pull v2 pendiente de autorización y paginación por ámbito." });
}
