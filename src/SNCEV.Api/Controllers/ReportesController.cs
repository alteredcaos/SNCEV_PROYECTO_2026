using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace SNCEV.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador,Capturador,Directivo,Auditor")]
[Route("v1/reportes")]
public sealed class ReportesController : ControllerBase
{
    private readonly IConfiguration _config;
    public ReportesController(IConfiguration config) => _config = config;

    private sealed record Filtros(DateTime Desde, DateTime Hasta, string? Clues);

    private IActionResult? Validar(DateTime? desde, DateTime? hasta, string? clues, out Filtros? filtros)
    {
        filtros = null;
        var inicio = (desde ?? new DateTime(DateTime.Today.Year, 1, 1)).Date;
        var fin = (hasta ?? DateTime.Today).Date;
        if (inicio > fin || (fin - inicio).TotalDays > 3660)
            return BadRequest(new { message = "Periodo inválido o superior a diez años." });
        var rolCapturador = User.IsInRole("Capturador") && !User.IsInRole("Administrador")
            && !User.IsInRole("Directivo") && !User.IsInRole("Auditor");
        var cluesJwt = User.FindFirstValue("clues")?.Trim();
        if (rolCapturador)
        {
            if (string.IsNullOrWhiteSpace(cluesJwt)) return Forbid();
            if (!string.IsNullOrWhiteSpace(clues) && !string.Equals(clues.Trim(), cluesJwt, StringComparison.OrdinalIgnoreCase))
                return Forbid();
            clues = cluesJwt;
        }
        if (clues?.Trim().Length > 20) return BadRequest(new { message = "CLUES inválida." });
        filtros = new Filtros(inicio, fin.AddDays(1), string.IsNullOrWhiteSpace(clues) ? null : clues.Trim());
        return null;
    }

    private async Task<SqlConnection> Abrir(CancellationToken ct)
    {
        var cs = _config.GetConnectionString("SqlServerConnection");
        if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Falta SqlServerConnection.");
        var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        return conn;
    }

    private const string Base = """
        FROM sncev.vacuna_aplicada va
        INNER JOIN sncev.vacuna_catalogo vc ON vc.id = va.vacuna_id
        WHERE va.anulada = 0
          AND va.fecha_aplicacion >= @Desde
          AND va.fecha_aplicacion < @Hasta
          AND (@Clues IS NULL OR va.unidad_medica_clues = @Clues)
        """;

    // Catálogo para el filtro: únicamente unidades con actividad registrada.
    // Capturador: limitado a la CLUES de su identidad autenticada.
    [HttpGet("catalogo-unidades")]
    public async Task<IActionResult> CatalogoUnidades(CancellationToken ct)
    {
        var error = Validar(null, null, null, out var f);
        if (error != null) return error;
        await using var conn = await Abrir(ct);
        const string sql = """
            SELECT a.clues AS Clues,
                   COALESCE(NULLIF(MAX(es.nombre_unidad), ''), a.clues) AS NombreUnidad
            FROM (SELECT DISTINCT unidad_medica_clues AS clues
                  FROM sncev.vacuna_aplicada
                  WHERE anulada = 0 AND (@Clues IS NULL OR unidad_medica_clues = @Clues)) a
            LEFT JOIN sncev_geo.establecimiento_salud es ON es.clues = a.clues
            GROUP BY a.clues
            ORDER BY NombreUnidad, a.clues;
            """;
        var filas = await conn.QueryAsync(new CommandDefinition(sql, new { f!.Clues }, cancellationToken: ct));
        return Ok(new { filas });
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] string? clues, CancellationToken ct)
    {
        var error = Validar(desde, hasta, clues, out var f);
        if (error != null) return error;
        await using var conn = await Abrir(ct);
        var sql = """
            SELECT COUNT_BIG(*) AS Aplicaciones,
                   COUNT(DISTINCT va.paciente_id) AS PacientesUnicos,
                   COUNT(DISTINCT va.vacuna_id) AS Biologicos,
                   COUNT(DISTINCT va.unidad_medica_clues) AS Unidades,
                   SUM(CASE WHEN va.aplicador_usuario_id IS NULL THEN CAST(1 AS BIGINT) ELSE CAST(0 AS BIGINT) END) AS SinVacunador
            """ + "\n" + Base;
        var totales = await conn.QuerySingleAsync(new CommandDefinition(sql, f, cancellationToken: ct));
        var tendencia = await conn.QueryAsync(new CommandDefinition("""
            SELECT CONVERT(char(7), va.fecha_aplicacion, 120) AS Mes,
                   COUNT(*) AS Aplicaciones, COUNT(DISTINCT va.paciente_id) AS PacientesUnicos
            """ + "\n" + Base + "\n" + """
            GROUP BY CONVERT(char(7), va.fecha_aplicacion, 120)
            ORDER BY Mes
            """, f, cancellationToken: ct));
        return Ok(new { desde = f!.Desde, hasta = f.Hasta.AddDays(-1), clues = f.Clues, totales, tendencia });
    }

    [HttpGet("biologicos")]
    public async Task<IActionResult> Biologicos([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] string? clues, CancellationToken ct)
    {
        var error = Validar(desde, hasta, clues, out var f);
        if (error != null) return error;
        await using var conn = await Abrir(ct);
        var sql = """
            SELECT va.vacuna_id AS VacunaId, vc.nombre_generico AS Biologico,
                   va.numero_dosis AS NumeroDosis, va.lote AS Lote,
                   COALESCE(lb.laboratorio, MAX(va.laboratorio)) AS Laboratorio,
                   va.unidad_medica_clues AS Clues,
                   lb.fecha_caducidad AS FechaCaducidad,
                   COUNT(*) AS Aplicaciones, COUNT(DISTINCT va.paciente_id) AS PacientesUnicos
            FROM sncev.vacuna_aplicada va
            INNER JOIN sncev.vacuna_catalogo vc ON vc.id = va.vacuna_id
            LEFT JOIN sncev.lote_biologico lb ON lb.vacuna_id = va.vacuna_id AND lb.lote = va.lote
            WHERE va.anulada = 0
              AND va.fecha_aplicacion >= @Desde AND va.fecha_aplicacion < @Hasta
              AND (@Clues IS NULL OR va.unidad_medica_clues = @Clues)
            GROUP BY va.vacuna_id, vc.nombre_generico, va.numero_dosis, va.lote,
                     va.unidad_medica_clues, lb.laboratorio, lb.fecha_caducidad
            ORDER BY Aplicaciones DESC, Biologico, Lote;
            """;
        var filas = await conn.QueryAsync(new CommandDefinition(sql, f, cancellationToken: ct));
        return Ok(new { filas });
    }

    [HttpGet("vacunadores")]
    public async Task<IActionResult> Vacunadores([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] string? clues, CancellationToken ct)
    {
        var error = Validar(desde, hasta, clues, out var f);
        if (error != null) return error;
        await using var conn = await Abrir(ct);
        var sql = """
            SELECT va.aplicador_usuario_id AS UsuarioId,
                   COALESCE(u.nombre_completo, 'Vacunador no identificado') AS Vacunador,
                   COUNT(*) AS Aplicaciones,
                   COUNT(DISTINCT va.paciente_id) AS PacientesUnicos,
                   COUNT(DISTINCT va.vacuna_id) AS Biologicos,
                   COUNT(DISTINCT va.fecha_aplicacion) AS DiasActivos,
                   CAST(COUNT(*) * 1.0 / NULLIF(COUNT(DISTINCT va.fecha_aplicacion), 0) AS decimal(12,2)) AS DosisPorDiaActivo
            FROM sncev.vacuna_aplicada va
            LEFT JOIN sncev_admin.usuario u ON u.id = va.aplicador_usuario_id
            WHERE va.anulada = 0
              AND va.fecha_aplicacion >= @Desde AND va.fecha_aplicacion < @Hasta
              AND (@Clues IS NULL OR va.unidad_medica_clues = @Clues)
            GROUP BY va.aplicador_usuario_id, u.nombre_completo
            ORDER BY Aplicaciones DESC, Vacunador;
            """;
        var filas = await conn.QueryAsync(new CommandDefinition(sql, f, cancellationToken: ct));
        return Ok(new { filas });
    }

    [HttpGet("unidades")]
    public async Task<IActionResult> Unidades([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] string? clues, CancellationToken ct)
    {
        var error = Validar(desde, hasta, clues, out var f);
        if (error != null) return error;
        await using var conn = await Abrir(ct);
        const string sql = """
            SELECT va.unidad_medica_clues AS Clues,
                   COUNT_BIG(*) AS Aplicaciones,
                   COUNT(DISTINCT va.paciente_id) AS PacientesUnicos,
                   COUNT(DISTINCT va.vacuna_id) AS Biologicos,
                   COUNT(DISTINCT va.fecha_aplicacion) AS DiasConActividad,
                   MIN(va.fecha_aplicacion) AS PrimeraAplicacion,
                   MAX(va.fecha_aplicacion) AS UltimaAplicacion
            FROM sncev.vacuna_aplicada va
            OUTER APPLY (SELECT TOP (1) NULLIF(nombre_unidad, '') AS nombre_unidad
                         FROM sncev_geo.establecimiento_salud
                         WHERE clues = va.unidad_medica_clues
                         ORDER BY nombre_unidad) es
            WHERE va.anulada = 0
              AND va.fecha_aplicacion >= @Desde AND va.fecha_aplicacion < @Hasta
              AND (@Clues IS NULL OR va.unidad_medica_clues = @Clues)
            GROUP BY va.unidad_medica_clues
            ORDER BY Aplicaciones DESC, Clues;
            """;
        const string sqlDetalle = """
            SELECT va.unidad_medica_clues AS Clues,
                   vc.nombre_generico AS Biologico,
                   COUNT_BIG(*) AS Aplicaciones,
                   COUNT(DISTINCT va.paciente_id) AS PacientesUnicos
            FROM sncev.vacuna_aplicada va
            INNER JOIN sncev.vacuna_catalogo vc ON vc.id = va.vacuna_id
            OUTER APPLY (SELECT TOP (1) NULLIF(nombre_unidad, '') AS nombre_unidad
                         FROM sncev_geo.establecimiento_salud
                         WHERE clues = va.unidad_medica_clues
                         ORDER BY nombre_unidad) es
            WHERE va.anulada = 0
              AND va.fecha_aplicacion >= @Desde AND va.fecha_aplicacion < @Hasta
              AND (@Clues IS NULL OR va.unidad_medica_clues = @Clues)
            GROUP BY va.unidad_medica_clues, vc.nombre_generico
            ORDER BY va.unidad_medica_clues, Aplicaciones DESC, Biologico;
            """;
        var filas = (await conn.QueryAsync(new CommandDefinition(sql, f, cancellationToken: ct))).ToList();
        var detalle = (await conn.QueryAsync(new CommandDefinition(sqlDetalle, f, cancellationToken: ct))).ToList();
        return Ok(new { filas, detalle });
    }

    // AGEB del domicilio vigente. No es cobertura poblacional oficial.
    [HttpGet("ageb")]
    public async Task<IActionResult> Ageb([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] string? clues, CancellationToken ct)
    {
        var error = Validar(desde, hasta, clues, out var f);
        if (error != null) return error;
        await using var conn = await Abrir(ct);
        const string sql = """
            WITH Domicilios AS (
                SELECT d.paciente_id, d.ageb_clave,
                       ROW_NUMBER() OVER (PARTITION BY d.paciente_id ORDER BY d.fecha_inicio DESC, d.fecha_registro DESC, d.id DESC) AS rn
                FROM sncev.domicilio_paciente d
                INNER JOIN sncev.paciente p ON p.id = d.paciente_id AND p.eliminado = 0
                WHERE d.vigente = 1
            ), Cohorte AS (
                SELECT d.paciente_id, d.ageb_clave
                FROM Domicilios d
                WHERE d.rn = 1 AND (
                    @Clues IS NULL OR EXISTS (
                        SELECT 1 FROM sncev.vacuna_aplicada x
                        WHERE x.paciente_id = d.paciente_id AND x.anulada = 0
                          AND x.unidad_medica_clues = @Clues
                          AND x.fecha_aplicacion >= @Desde AND x.fecha_aplicacion < @Hasta
                    )
                )
            ), Aplicaciones AS (
                SELECT va.paciente_id, va.id
                FROM sncev.vacuna_aplicada va
                WHERE va.anulada = 0
                  AND va.fecha_aplicacion >= @Desde AND va.fecha_aplicacion < @Hasta
                  AND (@Clues IS NULL OR va.unidad_medica_clues = @Clues)
            )
            SELECT COALESCE(NULLIF(RTRIM(c.ageb_clave), ''), 'SIN AGEB') AS Clave,
                   COALESCE(MAX(a.nombre), 'Sin AGEB identificado') AS Nombre,
                   COUNT(DISTINCT c.paciente_id) AS PacientesCaptados,
                   COUNT(DISTINCT ap.paciente_id) AS PacientesVacunados,
                   COUNT(DISTINCT ap.id) AS Aplicaciones,
                   CAST(100.0 * COUNT(DISTINCT ap.paciente_id) /
                        NULLIF(COUNT(DISTINCT c.paciente_id), 0) AS decimal(6,2)) AS PorcentajeConAplicacion
            FROM Cohorte c
            LEFT JOIN sncev_geo.ageb a ON a.clave = c.ageb_clave
            LEFT JOIN Aplicaciones ap ON ap.paciente_id = c.paciente_id
            GROUP BY COALESCE(NULLIF(RTRIM(c.ageb_clave), ''), 'SIN AGEB')
            ORDER BY PacientesCaptados DESC, Clave;
            """;
        var filas = await conn.QueryAsync(new CommandDefinition(sql, f, cancellationToken: ct));
        return Ok(new { filas, metodologia = "AGEB del domicilio vigente. Aplicaciones no anuladas en el periodo. Con filtro CLUES, cohorte limitada a pacientes con aplicación en esa unidad durante el periodo; sin filtro CLUES, todos los pacientes activos con domicilio vigente. No representa cobertura poblacional ni esquema completo." });
    }

}
