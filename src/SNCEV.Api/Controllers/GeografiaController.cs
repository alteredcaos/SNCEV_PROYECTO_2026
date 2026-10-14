using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNCEV.Api.Services.Geografia;

namespace SNCEV.Api.Controllers;

[ApiController]
[Route("v1/geografia")]
[Authorize(
    Roles =
        "Administrador,Capturador,Directivo,Auditor")]
public sealed class GeografiaController : ControllerBase
{
    private readonly IGeografiaService _geografia;

    public GeografiaController(
        IGeografiaService geografia)
    {
        _geografia = geografia;
    }

    [HttpGet("agebs/{cvegeo}")]
    [Produces("application/geo+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult ObtenerAgeb(string cvegeo)
    {
        if (string.IsNullOrWhiteSpace(cvegeo))
        {
            return BadRequest(new
            {
                message = "Debe proporcionar un CVEGEO."
            });
        }

        cvegeo = cvegeo.Trim();

        // La cobertura actual usa CVEGEO de 13 caracteres.
        if (cvegeo.Length != 13)
        {
            return BadRequest(new
            {
                message = "El CVEGEO debe contener 13 caracteres."
            });
        }

        var geoJson =
            _geografia.ObtenerAgebGeoJson(cvegeo);

        if (geoJson is null)
        {
            return NotFound(new
            {
                message =
                    $"No se encontró geometría para el AGEB {cvegeo}."
            });
        }

        return Content(
            geoJson,
            "application/geo+json");
    }

    [HttpGet("resolver")]
    [ProducesResponseType(
        typeof(ResolucionGeograficaDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Resolver(
        [FromQuery] double latitud,
        [FromQuery] double longitud,
        [FromQuery] int limiteClues = 5,
        CancellationToken cancellationToken = default)
    {
        if (latitud is < -90 or > 90)
        {
            return BadRequest(
                new
                {
                    message =
                        "La latitud debe estar entre -90 y 90."
                });
        }

        if (longitud is < -180 or > 180)
        {
            return BadRequest(
                new
                {
                    message =
                        "La longitud debe estar entre -180 y 180."
                });
        }

        if (limiteClues is < 1 or > 20)
        {
            return BadRequest(
                new
                {
                    message =
                        "limiteClues debe estar entre 1 y 20."
                });
        }

        var resultado =
            await _geografia.ResolverAsync(
                latitud,
                longitud,
                limiteClues,
                cancellationToken);

        return Ok(resultado);
    }
}