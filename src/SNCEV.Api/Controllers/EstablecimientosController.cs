
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNCEV.Api.Services.Establecimientos;

namespace SNCEV.Api.Controllers;

[ApiController]
[Route("v1/establecimientos")]
[Authorize(Roles = "Administrador,Capturador,Directivo,Auditor")]
public sealed class EstablecimientosController : ControllerBase
{
    private readonly IEstablecimientosService _service;

    public EstablecimientosController(
        IEstablecimientosService service)
        => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EstablecimientoDto>>> Buscar(
        [FromQuery] string? buscar = null,
        [FromQuery] int limite = 30,
        CancellationToken cancellationToken = default)
    {
        if (limite is < 1 or > 100)
            return BadRequest(new
            {
                message = "limite debe estar entre 1 y 100."
            });

        if (buscar?.Length > 100)
            return BadRequest(new
            {
                message = "buscar no debe superar 100 caracteres."
            });

        return Ok(await _service.BuscarAsync(
            buscar, limite, cancellationToken));
    }

    // Descarga completa para SQLite offline
    [HttpGet("catalogo")]
    public async Task<ActionResult<IReadOnlyList<EstablecimientoDto>>> Catalogo(
        CancellationToken cancellationToken)
    {
        var unidades = await _service.CatalogoAsync(cancellationToken);
        return Ok(unidades);
    }

    [HttpGet("{clues}")]
    public async Task<ActionResult<EstablecimientoDto>> Obtener(
        string clues,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clues) || clues.Length > 20)
            return BadRequest(new
            {
                message = "CLUES inválida."
            });

        var item = await _service.ObtenerAsync(
            clues.Trim(), cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }
}
