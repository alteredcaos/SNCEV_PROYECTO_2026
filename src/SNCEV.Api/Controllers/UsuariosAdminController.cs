using System.Security.Claims;
using System.Text.Json;
using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SNCEV.Api.Controllers;

/// <summary>Administración jurisdiccional de permisos y ámbitos; denegación por defecto.</summary>
[ApiController]
[Authorize]
[Route("v1/admin/usuarios")]
public sealed class UsuariosAdminController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UsuariosAdminController> _logger;
    private static readonly HashSet<string> Permisos = new(StringComparer.Ordinal)
    {
        "VACUNA_CONSULTAR", "VACUNA_REGISTRAR", "VACUNA_CORREGIR", "VACUNA_ANULAR",
        "PERSONAL_SUPERVISAR", "REPORTES_CONSULTAR", "USUARIOS_ADMINISTRAR", "PERMISOS_ADMINISTRAR"
    };

    public UsuariosAdminController(IConfiguration configuration, ILogger<UsuariosAdminController> logger)
    { _configuration = configuration; _logger = logger; }

    public sealed record AsignarPermisoDto(string Permiso, bool Activo, string Motivo);
    public sealed record AsignarAmbitoDto(string Tipo, string? Clues, bool Activo, string Motivo);

    private static bool MotivoValido(string? motivo) =>
        !string.IsNullOrWhiteSpace(motivo) && motivo.Trim().Length is >= 10 and <= 500;

    private static async Task GuardarAuditoria(SqlConnection conn, SqlTransaction tx,
        Guid actor, Guid destinatario, int jurisdiccion, string operacion,
        string recurso, object? anterior, object? nuevo, string motivo, CancellationToken ct)
    {
        const string sql = """
INSERT INTO sncev_audit.cambio_administrativo
(actor_id,destinatario_id,jurisdiccion_id,operacion,recurso,
 valor_anterior,valor_nuevo,motivo,solicitud_id)
VALUES (@Actor,@Destinatario,@Jurisdiccion,@Operacion,@Recurso,
 @Anterior,@Nuevo,@Motivo,@Solicitud);
""";
        await conn.ExecuteAsync(new CommandDefinition(sql, new {
            Actor=actor, Destinatario=destinatario, Jurisdiccion=jurisdiccion,
            Operacion=operacion, Recurso=recurso,
            Anterior=anterior is null ? null : JsonSerializer.Serialize(anterior),
            Nuevo=nuevo is null ? null : JsonSerializer.Serialize(nuevo),
            Motivo=motivo.Trim(), Solicitud=Guid.NewGuid()
        }, tx, cancellationToken:ct));
    }

    private Guid? ActorId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private SqlConnection Connection() => new(_configuration.GetConnectionString("SqlServerConnection")
        ?? throw new InvalidOperationException("SqlServerConnection no configurada"));

    // Un actor debe poseer permiso administrativo y ámbito jurisdiccional activo.
    // No se acepta el rol JWT como sustituto de permisos persistidos.
    private static async Task<int?> JurisdiccionActor(SqlConnection conn, Guid actor, string permiso, SqlTransaction? tx = null)
    {
        var ids = (await conn.QueryAsync<int>(@"
SELECT DISTINCT a.ambito_id
FROM sncev_admin.usuario u WITH (UPDLOCK, HOLDLOCK)
JOIN sncev_admin.usuario_permiso p WITH (UPDLOCK, HOLDLOCK) ON p.usuario_id=u.id
JOIN sncev_admin.usuario_ambito a WITH (UPDLOCK, HOLDLOCK) ON a.usuario_id=u.id
JOIN sncev_geo.ambito_jurisdiccional j WITH (HOLDLOCK) ON j.id=a.ambito_id
WHERE u.id=@Actor AND u.activo=1 AND p.permiso=@Permiso AND p.activo=1
  AND a.tipo='JURISDICCION' AND a.activo=1 AND j.activo=1
  AND a.vigente_desde<=SYSUTCDATETIME()
  AND (a.vigente_hasta IS NULL OR a.vigente_hasta>SYSUTCDATETIME())",
            new { Actor=actor, Permiso=permiso }, tx)).ToList();
        // Ambigüedad territorial: negar en lugar de elegir arbitrariamente.
        return ids.Count == 1 ? ids[0] : null;
    }

    // Impide administrar usuarios con ámbitos fuera de la jurisdicción del actor.
    private static async Task<bool> DestinatarioPermitido(SqlConnection conn, Guid usuario, int jurisdiccion, SqlTransaction? tx = null)
    {
        if (!await conn.ExecuteScalarAsync<bool>(@"
SELECT CAST(CASE WHEN EXISTS(
    SELECT 1
    FROM sncev_admin.usuario WITH (UPDLOCK,HOLDLOCK)
    WHERE id=@Usuario
) THEN 1 ELSE 0 END AS bit)",
    new { Usuario = usuario }, tx)) return false;

        // Los usuarios sin ámbito solo pueden gestionarse si su adscripción está registrada
        // explícitamente en esta jurisdicción. No se acepta cualquier usuario global.
        var adscrito = await conn.ExecuteScalarAsync<bool>(@"
SELECT CAST(CASE WHEN EXISTS (
    SELECT 1
    FROM sncev_admin.usuario u WITH (UPDLOCK,HOLDLOCK)
    WHERE u.id = @Usuario
      AND (
          EXISTS (
              SELECT 1 FROM sncev_geo.ambito_clues ac WITH (HOLDLOCK)
              WHERE ac.ambito_id = @Jurisdiccion
                AND ac.clues = u.clues_asignada AND ac.activo = 1
          )
          OR EXISTS (
              SELECT 1 FROM sncev_admin.usuario_ambito a WITH (UPDLOCK,HOLDLOCK)
              WHERE a.usuario_id = u.id AND a.activo = 1
                AND a.vigente_desde <= SYSUTCDATETIME()
                AND (a.vigente_hasta IS NULL OR a.vigente_hasta > SYSUTCDATETIME())
                AND (
                    (a.tipo = 'JURISDICCION' AND a.ambito_id = @Jurisdiccion)
                    OR (a.tipo IN ('UNIDAD', 'COORDINACION') AND EXISTS (
                        SELECT 1 FROM sncev_geo.ambito_clues ac WITH (HOLDLOCK)
                        WHERE ac.ambito_id = @Jurisdiccion
                          AND ac.clues = a.clues AND ac.activo = 1
                    ))
                )
          )
      )
) THEN 1 ELSE 0 END AS bit)", new { Usuario = usuario, Jurisdiccion = jurisdiccion }, tx);
        if (!adscrito) return false;
        return !await conn.ExecuteScalarAsync<bool>(@"
SELECT CAST(CASE WHEN EXISTS(
 SELECT 1 FROM sncev_admin.usuario_ambito a WITH (UPDLOCK,HOLDLOCK)
 WHERE a.usuario_id=@Usuario AND a.activo=1
 AND (a.vigente_hasta IS NULL OR a.vigente_hasta>SYSUTCDATETIME())
 AND (
   (a.tipo='JURISDICCION' AND a.ambito_id<>@Jurisdiccion)
   OR (a.tipo IN ('UNIDAD','COORDINACION') AND NOT EXISTS(
       SELECT 1 FROM sncev_geo.ambito_clues ac WITH (HOLDLOCK)
       WHERE ac.ambito_id=@Jurisdiccion AND ac.clues=a.clues AND ac.activo=1))
 )) THEN 1 ELSE 0 END AS bit)", new { Usuario=usuario, Jurisdiccion=jurisdiccion }, tx);
    }

    // Validación autoritativa dentro de la transacción SERIALIZABLE.
    // Los bloqueos se conservan hasta COMMIT/ROLLBACK y cubren revocaciones concurrentes.
    private static async Task<int?> AutorizarCambio(SqlConnection conn, SqlTransaction tx,
        Guid actor, Guid destinatario, CancellationToken ct)
    {
        foreach (var id in new[] { actor, destinatario }.OrderBy(x => x))
        {
            var existe = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(*) FROM sncev_admin.usuario WITH (UPDLOCK,HOLDLOCK) WHERE id=@Id",
                new { Id=id }, tx, cancellationToken:ct));
            if (existe != 1) return null;
        }
        var j = await JurisdiccionActor(conn, actor, "PERMISOS_ADMINISTRAR", tx);
        if (!j.HasValue || !await DestinatarioPermitido(conn, destinatario, j.Value, tx)) return null;
        return j;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var actor = ActorId();
        if (!actor.HasValue) return Forbid();
        await using var conn = Connection();
        await conn.OpenAsync(ct);
        var jurisdiccion = await JurisdiccionActor(conn, actor.Value, "USUARIOS_ADMINISTRAR");
        if (!jurisdiccion.HasValue) return Forbid();
        // No listar usuarios ajenos, ni revelar credenciales o CURP.
        var usuarios = await conn.QueryAsync(@"
SELECT DISTINCT u.id, u.username, u.nombre_completo, u.clues_asignada, u.activo
FROM sncev_admin.usuario u
JOIN sncev_admin.usuario_ambito a ON a.usuario_id=u.id AND a.activo=1
WHERE u.activo=1 AND (a.vigente_hasta IS NULL OR a.vigente_hasta>SYSUTCDATETIME())
AND ((a.tipo='JURISDICCION' AND a.ambito_id=@Jurisdiccion)
 OR (a.tipo IN ('UNIDAD','COORDINACION') AND EXISTS(
  SELECT 1 FROM sncev_geo.ambito_clues ac
  WHERE ac.ambito_id=@Jurisdiccion AND ac.clues=a.clues AND ac.activo=1)))
ORDER BY u.username", new { Jurisdiccion=jurisdiccion.Value });
        return Ok(usuarios);
    }

    [HttpGet("{usuarioId:guid}/permisos")]
    public async Task<IActionResult> ConsultarPermisos(Guid usuarioId, CancellationToken ct)
    {
        var actor=ActorId(); if (!actor.HasValue) return Forbid();
        await using var conn=Connection(); await conn.OpenAsync(ct);
        var j=await JurisdiccionActor(conn,actor.Value,"PERMISOS_ADMINISTRAR");
        if (!j.HasValue || !await DestinatarioPermitido(conn,usuarioId,j.Value)) return Forbid();
        var permisos=await conn.QueryAsync(@"SELECT permiso,activo FROM sncev_admin.usuario_permiso
WHERE usuario_id=@Usuario ORDER BY permiso",new { Usuario=usuarioId });
        return Ok(permisos);
    }

    [HttpGet("{usuarioId:guid}/auditoria")]
    public async Task<IActionResult> ConsultarAuditoria(Guid usuarioId, CancellationToken ct)
    {
        var actor=ActorId(); if (!actor.HasValue) return Forbid();
        await using var conn=Connection(); await conn.OpenAsync(ct);
        var j=await JurisdiccionActor(conn,actor.Value,"PERMISOS_ADMINISTRAR");
        if (!j.HasValue || !await DestinatarioPermitido(conn,usuarioId,j.Value)) return Forbid();
        var registros=await conn.QueryAsync(new CommandDefinition(@"
SELECT TOP (100) id,fecha_utc,actor_id,destinatario_id,operacion,recurso,
valor_anterior,valor_nuevo,motivo,solicitud_id
FROM sncev_audit.cambio_administrativo
WHERE destinatario_id=@Usuario AND jurisdiccion_id=@J
ORDER BY fecha_utc DESC,id DESC",
            new { Usuario=usuarioId,J=j.Value },cancellationToken:ct));
        return Ok(registros);
    }

    [HttpPut("{usuarioId:guid}/permisos")]
    public async Task<IActionResult> ConfigurarPermiso(Guid usuarioId, [FromBody] AsignarPermisoDto dto, CancellationToken ct)
    {
        if (dto is null || !Permisos.Contains(dto.Permiso ?? "") || !MotivoValido(dto.Motivo)) return BadRequest(new { error="PERMISO_INVALIDO" });
        var actor=ActorId(); if (!actor.HasValue) return Forbid();
        if (actor.Value==usuarioId) return Conflict(new { error="AUTOMODIFICACION_NO_PERMITIDA" });
        await using var conn=Connection(); await conn.OpenAsync(ct);
        // Transacción serializable y segunda verificación para evitar cambios de ámbito concurrentes.
        using var tx=conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        try
        {
            var j=await AutorizarCambio(conn,tx,actor.Value,usuarioId,ct);
            if (!j.HasValue) { tx.Rollback(); return Forbid(); }
            // Bloquear filas de ámbito del destinatario antes de escribir.
            await conn.ExecuteAsync(@"SELECT COUNT(*) FROM sncev_admin.usuario_ambito WITH (UPDLOCK,HOLDLOCK)
WHERE usuario_id=@Usuario",new { Usuario=usuarioId },tx);
            var anterior = await conn.QuerySingleOrDefaultAsync<bool?>(new CommandDefinition(@"
SELECT activo FROM sncev_admin.usuario_permiso WITH (UPDLOCK,HOLDLOCK)
WHERE usuario_id=@Usuario AND permiso=@Permiso",
                new { Usuario=usuarioId,Permiso=dto.Permiso },tx,cancellationToken:ct));
            if (anterior != dto.Activo)
            {
                await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE sncev_admin.usuario_permiso SET activo=@Activo WHERE usuario_id=@Usuario AND permiso=@Permiso;
IF @@ROWCOUNT=0 INSERT INTO sncev_admin.usuario_permiso(usuario_id,permiso,activo)
VALUES(@Usuario,@Permiso,@Activo);",
                    new { Usuario=usuarioId,Permiso=dto.Permiso,Activo=dto.Activo },tx,cancellationToken:ct));
                await GuardarAuditoria(conn,tx,actor.Value,usuarioId,j.Value,
                    "PERMISO_ACTUALIZADO",dto.Permiso,
                    anterior.HasValue ? new { activo=anterior.Value } : null,
                    new { activo=dto.Activo },dto.Motivo,ct);
            }
            tx.Commit();
            _logger.LogInformation("Cambio de permiso usuario={Usuario} permiso={Permiso} activo={Activo} actor={Actor}",
                usuarioId,dto.Permiso,dto.Activo,actor.Value);
            return Ok(new { usuarioId, dto.Permiso, dto.Activo });
        }
        catch { try { tx.Rollback(); } catch (InvalidOperationException) { } throw; }
    }

    [HttpPut("{usuarioId:guid}/ambito")]
    public async Task<IActionResult> ConfigurarAmbito(Guid usuarioId, [FromBody] AsignarAmbitoDto dto, CancellationToken ct)
    {
        if (dto is null || dto.Tipo is not ("UNIDAD" or "COORDINACION") ||
            string.IsNullOrWhiteSpace(dto.Clues) || dto.Clues.Length>20 || !MotivoValido(dto.Motivo))
            return BadRequest(new { error="AMBITO_INVALIDO" });
        var actor=ActorId(); if (!actor.HasValue) return Forbid();
        if (actor.Value==usuarioId) return Conflict(new { error="AUTOMODIFICACION_NO_PERMITIDA" });
        await using var conn=Connection(); await conn.OpenAsync(ct);
        var clues=dto.Clues.Trim().ToUpperInvariant();
        // No se modifica aquí el ámbito jurisdiccional: requiere flujo de autorización separado.
        using var tx=conn.BeginTransaction(System.Data.IsolationLevel.Serializable);
        try
        {
            var j=await AutorizarCambio(conn,tx,actor.Value,usuarioId,ct);
            if (!j.HasValue) { tx.Rollback(); return Forbid(); }
            var valida=await conn.ExecuteScalarAsync<bool>(@"
    SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM sncev_geo.ambito_clues ac WITH (HOLDLOCK)
    JOIN sncev_geo.establecimiento_salud e WITH (HOLDLOCK) ON e.clues=ac.clues
    WHERE ac.ambito_id=@J AND ac.clues=@Clues AND ac.activo=1)
    THEN 1 ELSE 0 END AS bit)",new { J=j.Value,Clues=clues },tx);
            if (!valida) { tx.Rollback(); return BadRequest(new { error="CLUES_FUERA_JURISDICCION" }); }
            // Evitar altas ambiguas de coordinación sobre una unidad no designada como coordinadora.
            if (dto.Tipo=="COORDINACION")
            {
                var esCoordinador=await conn.ExecuteScalarAsync<bool>(@"
    SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM sncev_geo.establecimiento_jerarquia WITH (HOLDLOCK)
    WHERE clues_padre=@Clues AND activo=1) THEN 1 ELSE 0 END AS bit)",new { Clues=clues },tx);
                if (!esCoordinador) { tx.Rollback(); return BadRequest(new { error="CLUES_NO_ES_COORDINADOR" }); }
            }
            // Denegar nuevos ámbitos que no correspondan al territorio actual.
            var existentes=(await conn.QueryAsync<(long Id, bool Activo)>(new CommandDefinition(@"
SELECT id AS Id, activo AS Activo FROM sncev_admin.usuario_ambito WITH (UPDLOCK,HOLDLOCK)
WHERE usuario_id=@Usuario AND tipo=@Tipo AND clues=@Clues AND vigente_hasta IS NULL",
                new { Usuario=usuarioId,Tipo=dto.Tipo,Clues=clues },tx,cancellationToken:ct))).ToArray();
            var cambio = existentes.Length == 0
    || existentes.Any(x => x.Activo != dto.Activo);
            if (cambio)
            {
                if (existentes.Length>0)
                    await conn.ExecuteAsync(new CommandDefinition(@"UPDATE sncev_admin.usuario_ambito SET activo=@Activo
WHERE id IN @Ids",new { Activo=dto.Activo,Ids=existentes.Select(x=>x.Id).ToArray() },tx,cancellationToken:ct));
                else
                    await conn.ExecuteAsync(new CommandDefinition(@"INSERT sncev_admin.usuario_ambito(usuario_id,tipo,clues,activo)
VALUES(@Usuario,@Tipo,@Clues,@Activo)",
new
{
    Usuario = usuarioId,
    Tipo = dto.Tipo,
    Clues = clues,
    Activo = dto.Activo
}, tx,cancellationToken:ct));
                await GuardarAuditoria(conn,tx,actor.Value,usuarioId,j.Value,
                    "AMBITO_ACTUALIZADO",dto.Tipo+":"+clues,
                    new { registros=existentes.Select(x=>new { x.Id,x.Activo }).ToArray() },
                    new { tipo=dto.Tipo,clues,activo=dto.Activo },dto.Motivo,ct);
            }
            tx.Commit();
            _logger.LogInformation("Cambio de ámbito usuario={Usuario} tipo={Tipo} clues={Clues} activo={Activo} actor={Actor}",
                usuarioId,dto.Tipo,clues,dto.Activo,actor.Value);
            return Ok(new { usuarioId,dto.Tipo,clues,dto.Activo });
        }
        catch { try { tx.Rollback(); } catch (InvalidOperationException) { } throw; }
    }
}
