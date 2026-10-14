using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SNCEV.Api.Controllers;

/// <summary>Resolución conservadora: sólo vincular pacientes centrales existentes o descartar.
/// Nunca crea pacientes ni registra vacunas. Requiere revisión humana previa.</summary>
[ApiController]
[Authorize(Roles = "Administrador")]
[Route("v1/sync/v2/conflictos")]
public sealed class SyncConflictosController : ControllerBase
{
    private readonly IConfiguration _config;
    public SyncConflictosController(IConfiguration config) => _config = config;

    public sealed record ResolverRequest(string Accion, Guid? PacienteCentralId, string Motivo);

    private bool TryActor(out Guid actor, out string clues)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        clues = User.FindFirstValue("clues") ?? User.FindFirstValue("clues_asignada") ?? "";
        return Guid.TryParse(raw, out actor) && !string.IsNullOrWhiteSpace(clues);
    }

    [HttpGet]
    public async Task<IActionResult> Pendientes([FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (!TryActor(out _, out var clues)) return Forbid();
        if (take < 1 || take > 100) return BadRequest(new { error = "take debe estar entre 1 y 100" });
        await using var db = new SqlConnection(_config.GetConnectionString("SqlServerConnection"));
        var rows = await db.QueryAsync(new CommandDefinition(@"
SELECT TOP (@Take) c.conflicto_id AS ConflictoId, c.operacion_id AS OperacionId,
       c.tipo AS Tipo, c.descripcion AS Descripcion, c.estado AS Estado,
       c.fecha_creacion AS FechaCreacionUtc, o.entidad AS Entidad,
       o.entidad_local_id AS EntidadLocalId, o.dispositivo_id AS DispositivoId,
       o.codigo_error AS Codigo, o.clues AS Clues
FROM sncev_sync.conflicto c
JOIN sncev_sync.operacion o ON o.operacion_id=c.operacion_id
WHERE c.estado='Pendiente' AND o.estado='Conflicto' AND o.clues=@Clues
ORDER BY c.fecha_creacion, c.conflicto_id", new { Take=take, Clues=clues }, cancellationToken:ct));
        return Ok(rows);
    }

    [HttpPost("{conflictoId:long}/resolver")]
    public async Task<IActionResult> Resolver(long conflictoId, [FromBody] ResolverRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor, out var clues)) return Forbid();
        if (request is null || string.IsNullOrWhiteSpace(request.Motivo) ||
            request.Motivo.Trim().Length < 10 || request.Motivo.Length > 1000)
            return BadRequest(new { error = "Motivo obligatorio de 10 a 1000 caracteres" });
        if (request.Accion is not ("VincularExistente" or "Descartar"))
            return BadRequest(new { error = "Acción no permitida" });
        if (request.Accion == "VincularExistente" && (!request.PacienteCentralId.HasValue || request.PacienteCentralId == Guid.Empty))
            return BadRequest(new { error = "Indica pacienteCentralId" });
        if (request.Accion == "Descartar" && request.PacienteCentralId.HasValue)
            return BadRequest(new { error = "Descartar no admite pacienteCentralId" });

        await using var db = new SqlConnection(_config.GetConnectionString("SqlServerConnection"));
        await db.OpenAsync(ct);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var c = await db.QuerySingleOrDefaultAsync<ConflictoRow>(new CommandDefinition(@"
SELECT c.conflicto_id AS ConflictoId, c.operacion_id AS OperacionId,
       c.estado AS EstadoConflicto, o.estado AS EstadoOperacion,
       o.entidad AS Entidad, o.clues AS Clues, o.dispositivo_id AS DispositivoId,
       o.entidad_local_id AS EntidadLocalId, o.usuario_id AS UsuarioOrigenId
FROM sncev_sync.conflicto c WITH(UPDLOCK,HOLDLOCK)
JOIN sncev_sync.operacion o WITH(UPDLOCK,HOLDLOCK) ON o.operacion_id=c.operacion_id
WHERE c.conflicto_id=@Id", new { Id=conflictoId },tx,cancellationToken:ct));
            if (c is null || !string.Equals(c.Clues,clues,StringComparison.OrdinalIgnoreCase))
            { tx.Rollback(); return NotFound(); }
            if (c.UsuarioOrigenId==actor)
            { tx.Rollback(); return Conflict(new {error="El capturista no puede aprobar su propia operación"}); }
            if (c.EstadoConflicto!="Pendiente" || c.EstadoOperacion!="Conflicto")
            { tx.Rollback(); return Conflict(new { error="Conflicto ya resuelto o estado inconsistente" }); }
            if (c.Entidad!="Paciente")
            { tx.Rollback(); return Conflict(new { error="Resolución clínica de vacunas aún no habilitada" }); }

            Guid? central = null;
            string newState;
            if (request.Accion=="VincularExistente")
            {
                central=request.PacienteCentralId;
                var exists=await db.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM sncev.paciente WITH(HOLDLOCK)
WHERE id=@Id AND eliminado=0",new {Id=central},tx,cancellationToken:ct));
                if (exists!=1) { tx.Rollback(); return Conflict(new {error="Paciente central inexistente o inactivo"}); }
                var mapped=await db.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(@"
SELECT paciente_central_id FROM sncev_sync.paciente_equivalencia WITH(UPDLOCK,HOLDLOCK)
WHERE dispositivo_id=@Device AND paciente_local_id=@Local",
                    new {Device=c.DispositivoId,Local=c.EntidadLocalId},tx,cancellationToken:ct));
                if (mapped.HasValue && mapped.Value!=central)
                { tx.Rollback(); return Conflict(new {error="Existe una equivalencia distinta; revisión manual requerida"}); }
                if (!mapped.HasValue)
                    await db.ExecuteAsync(new CommandDefinition(@"
INSERT INTO sncev_sync.paciente_equivalencia
(dispositivo_id,paciente_local_id,paciente_central_id,operacion_id)
VALUES (@Device,@Local,@Central,@Op)",new {Device=c.DispositivoId,Local=c.EntidadLocalId,Central=central,Op=c.OperacionId},tx,cancellationToken:ct));
                newState="Confirmada";
            }
            else newState="Rechazada";

            await db.ExecuteAsync(new CommandDefinition(@"
UPDATE sncev_sync.operacion SET estado=@State, entidad_central_id=@Central,
 codigo_error=@Code, fecha_resolucion=SYSUTCDATETIME() WHERE operacion_id=@Op;
UPDATE sncev_sync.conflicto SET estado=@ConflictState, resuelto_por=@Actor,
 fecha_resolucion=SYSUTCDATETIME() WHERE conflicto_id=@Id;
INSERT INTO sncev_sync.resolucion_auditoria
(conflicto_id,operacion_id,accion,estado_anterior,estado_nuevo,paciente_central_id,usuario_id,motivo)
VALUES (@Id,@Op,@Action,'Conflicto',@State,@Central,@Actor,@Reason);",
                new {State=newState,Central=central,Code=request.Accion=="Descartar"?"DESCARTADA_POR_REVISOR":(string?)null,
                    ConflictState=request.Accion=="Descartar"?"Descartado":"Resuelto",Actor=actor,
                    Id=conflictoId,Op=c.OperacionId,Action=request.Accion,Reason=request.Motivo.Trim()},tx,cancellationToken:ct));
            tx.Commit();
            return Ok(new {conflictoId,operacionId=c.OperacionId,estado=newState,entidadCentralId=central});
        }
        catch { tx.Rollback(); throw; }
    }

    private sealed class ConflictoRow
    {
        public long ConflictoId {get;set;}
        public Guid OperacionId {get;set;}
        public string EstadoConflicto {get;set;}="";
        public string EstadoOperacion {get;set;}="";
        public string Entidad {get;set;}="";
        public string Clues {get;set;}="";
        public Guid DispositivoId {get;set;}
        public Guid EntidadLocalId {get;set;}
        public Guid UsuarioOrigenId {get;set;}
    }
}
