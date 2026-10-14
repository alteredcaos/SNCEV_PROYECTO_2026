using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SNCEV.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/catalogo")]
public class CatalogoController : ControllerBase
{
    private readonly IConfiguration _config;

    public CatalogoController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("estados")]
    public async Task<IActionResult> ObtenerEstados()
    {
        await using var conn = new SqlConnection(GetConnectionString());
        var estados = await conn.QueryAsync(@"
            SELECT clave_inegi AS ClaveInegi, nombre AS Nombre
            FROM sncev_geo.estado
            ORDER BY nombre");

        return Ok(estados);
    }

    [HttpGet("tipos-cartilla")]
    public async Task<IActionResult> ObtenerTiposCartilla()
    {
        await using var conn = new SqlConnection(GetConnectionString());
        var tipos = await conn.QueryAsync(@"
            SELECT id AS Id, nombre AS Nombre, edad_minima AS EdadMinima,
                   edad_maxima AS EdadMaxima, sexo AS Sexo
            FROM sncev.tipo_cartilla
            WHERE activo = 1
            ORDER BY edad_minima, id");

        return Ok(tipos);
    }

    [HttpGet("municipios")]
    public async Task<IActionResult> ObtenerMunicipios([FromQuery] string? estadoClave)
    {
        await using var conn = new SqlConnection(GetConnectionString());
        var clave = string.IsNullOrWhiteSpace(estadoClave) ? null : estadoClave.Trim();

        var municipios = await conn.QueryAsync(@"
            SELECT id AS Id, clave_inegi AS ClaveInegi, nombre AS Nombre, estado_clave AS EstadoClave
            FROM sncev_geo.municipio
            WHERE (@EstadoClave IS NULL OR estado_clave = @EstadoClave)
            ORDER BY nombre", new { EstadoClave = clave });

        return Ok(municipios);
    }

    private string GetConnectionString() =>
        _config.GetConnectionString("SqlServerConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:SqlServerConnection no está configurada.");
}
