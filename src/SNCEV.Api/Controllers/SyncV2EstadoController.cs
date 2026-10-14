using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SNCEV.Api.Controllers;

/// <summary>Consulta limitada a operaciones del usuario/dispositivo autenticados.
/// No es un pull nominal ni entrega información clínica.</summary>
[ApiController]
[Authorize]
[Route("v1/sync/v2/estado")]
public sealed class SyncV2EstadoController : ControllerBase
{
    private readonly IConfiguration _config;
    public SyncV2EstadoController(IConfiguration config) => _config=config;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid dispositivoId,
        [FromQuery] int take=50, CancellationToken ct=default)
    {
        var raw=User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var clues=User.FindFirstValue("clues") ?? User.FindFirstValue("clues_asignada");
        if (!Guid.TryParse(raw,out var userId) || string.IsNullOrWhiteSpace(clues)) return Forbid();
        if (dispositivoId==Guid.Empty || take<1 || take>100) return BadRequest();
        await using var db=new SqlConnection(_config.GetConnectionString("SqlServerConnection"));
        var result=await db.QueryAsync(new CommandDefinition(@"
SELECT TOP (@Take) operacion_id AS OperacionId, estado AS Estado,
 entidad_central_id AS EntidadCentralId, codigo_error AS Codigo,
 fecha_resolucion AS FechaResolucionUtc
FROM sncev_sync.operacion
WHERE dispositivo_id=@Device AND usuario_id=@UserId AND clues=@Clues
ORDER BY fecha_recepcion DESC, operacion_id DESC",
          new {Take=take,Device=dispositivoId,UserId=userId,Clues=clues},cancellationToken:ct));
        return Ok(result);
    }
}
