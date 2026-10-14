using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNCEV.Api.Services;
using System.Security.Claims;
using static SNCEV.Api.Services.CartillaPdfService;
namespace SNCEV.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/pacientes/{pacienteId:guid}/cartilla")]
public sealed class CartillaController : ControllerBase
{
    private readonly CartillaPdfService _pdfService;

    public CartillaController(CartillaPdfService pdfService)
    {
        _pdfService = pdfService;
    }

    [HttpGet("pdf")]
    [Authorize(Roles = "Administrador,Capturador,Directivo,Auditor")]
    [Produces("application/pdf")]
    public async Task<IActionResult> DescargarPdf(
        Guid pacienteId,
        CancellationToken cancellationToken)
    {
        var usuarioIdTexto =
     User.FindFirstValue(ClaimTypes.NameIdentifier)
     ?? User.FindFirstValue("sub");

        Guid? usuarioId = null;

        if (Guid.TryParse(usuarioIdTexto, out var idParseado))
        {
            usuarioId = idParseado;
        }

        var nombre =
            User.FindFirstValue("nombre")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.Identity?.Name;

        var clues =
            User.FindFirstValue("clues");

        var emisor = new EmisorCartilla(
            usuarioId,
            nombre,
            clues);

        var resultado =
            await _pdfService.GenerarAsync(
                pacienteId,
                emisor,
                cancellationToken);

        if (resultado is null)
        {
            return NotFound(new
            {
                message = "No se encontró un paciente activo con ese identificador."
            });
        }
        Response.Headers.CacheControl =
    "no-store, no-cache, must-revalidate";

        Response.Headers.Pragma = "no-cache";
        return File(
            resultado.Contenido,
            "application/pdf",
            resultado.NombreArchivo);
    }
}
