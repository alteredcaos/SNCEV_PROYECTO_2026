using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace SNCEV.Api.Services;

/// <summary>
/// Autorización territorial explícita, denegación por defecto.
/// Requiere migración 3C.3B y asignación administrativa de ámbitos y permisos.
/// </summary>
public sealed class AutorizacionCluesService
{
    private readonly IConfiguration _configuration;
    public AutorizacionCluesService(IConfiguration configuration) => _configuration = configuration;

    public async Task<bool> PuedeAsync(ClaimsPrincipal principal, string clues, string permiso,
        CancellationToken cancellationToken = default)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var usuarioId) || string.IsNullOrWhiteSpace(clues)
            || string.IsNullOrWhiteSpace(permiso)) return false;

        await using var conn = new SqlConnection(
            _configuration.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException("Falta SqlServerConnection"));
        await conn.OpenAsync(cancellationToken);

        const string sql = """
SELECT CAST(CASE WHEN EXISTS (
 SELECT 1 FROM sncev_admin.usuario u
 JOIN sncev_admin.usuario_permiso p ON p.usuario_id=u.id
 JOIN sncev_admin.usuario_ambito a ON a.usuario_id=u.id
 WHERE u.id=@Usuario AND u.activo=1 AND p.activo=1 AND p.permiso=@Permiso
 AND a.activo=1 AND a.vigente_desde<=SYSUTCDATETIME()
 AND (a.vigente_hasta IS NULL OR a.vigente_hasta>SYSUTCDATETIME())
 AND (
   (a.tipo='UNIDAD' AND a.clues=@Clues)
   OR (a.tipo='JURISDICCION' AND EXISTS (
     SELECT 1 FROM sncev_geo.ambito_clues ac
     JOIN sncev_geo.ambito_jurisdiccional j ON j.id=ac.ambito_id
     WHERE ac.ambito_id=a.ambito_id AND ac.clues=@Clues
       AND ac.activo=1 AND j.activo=1))
   OR (a.tipo='COORDINACION' AND (
     a.clues=@Clues OR EXISTS (
       SELECT 1 FROM sncev_geo.establecimiento_jerarquia h
       WHERE h.clues_padre=a.clues AND h.clues_hija=@Clues AND h.activo=1)))
 )
) THEN 1 ELSE 0 END AS bit)
""";
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(sql,
            new { Usuario=usuarioId, Clues=clues.Trim(), Permiso=permiso.Trim() },
            cancellationToken:cancellationToken));
    }
}
