using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNCEV.Api.Services;

namespace SNCEV.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/planes-recorrido")]
public sealed class PlanRecorridosController : ControllerBase
{
    private readonly PlanRecorridoPdfService _pdfService;

    public PlanRecorridosController(
        PlanRecorridoPdfService pdfService)
    {
        _pdfService = pdfService;
    }


    // =========================================================
    // GET /v1/planes-recorrido/pdf?fecha=2026-10-09
    // =========================================================

    [HttpGet("pdf")]
    [Authorize(Roles =
        "Administrador,Capturador,Directivo,Auditor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DescargarPdf(
        [FromQuery] DateOnly fecha,
        [FromQuery] string? clues = null,
        CancellationToken cancellationToken = default)
    {
        var rol =
            User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role");

        var cluesJwt =
            User.FindFirstValue("clues");


        // -----------------------------------------------------
        // Capturador:
        // solamente puede generar el recorrido de su CLUES.
        // -----------------------------------------------------

        if (string.Equals(
                rol,
                "Capturador",
                StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(cluesJwt))
                return Forbid();

            clues = cluesJwt;
        }


        // -----------------------------------------------------
        // Para los demás roles:
        //
        // Si no se especifica CLUES, usamos la del JWT.
        // -----------------------------------------------------

        if (string.IsNullOrWhiteSpace(clues))
            clues = cluesJwt;

        if (string.IsNullOrWhiteSpace(clues))
        {
            return BadRequest(new
            {
                message =
                    "Debe especificarse una CLUES."
            });
        }


        var resultado =
            await _pdfService.GenerarAsync(
                fecha.ToDateTime(TimeOnly.MinValue),
                clues,
                cancellationToken);

        if (resultado is null)
        {
            return NotFound(new
            {
                message =
                    "No existen citas PROGRAMADA o CONFIRMADA " +
                    "para la fecha y CLUES indicadas.",

                fecha,
                clues
            });
        }


        Response.Headers.CacheControl =
            "no-store, no-cache, must-revalidate";

        Response.Headers.Pragma =
            "no-cache";

        return File(
            resultado.Contenido,
            "application/pdf",
            resultado.NombreArchivo);
    }
}