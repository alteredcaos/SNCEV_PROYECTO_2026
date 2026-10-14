using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNCEV.Api.Services;

namespace SNCEV.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador,Directivo,Auditor")]
[Route("v1/reportes/censo")]
public sealed class CensoEvaluacionController : ControllerBase
{
    private readonly EvaluadorCoberturaNominalService _evaluador;

    public CensoEvaluacionController(EvaluadorCoberturaNominalService evaluador)
    {
        _evaluador = evaluador;
    }

    /// <summary>
    /// Evaluación nominal exploratoria. No certifica esquema completo ni cobertura oficial.
    /// El acceso por ámbito territorial deberá reforzarse antes de habilitar Capturador.
    /// </summary>
    [HttpGet("evaluar/{pacienteId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Evaluar(
        Guid pacienteId,
        [FromQuery] DateTime? fechaCorte,
        CancellationToken ct)
    {
        if (pacienteId == Guid.Empty)
            return BadRequest(new { mensaje = "El identificador del paciente no es válido." });

        var fecha = (fechaCorte ?? DateTime.Today).Date;
        if (fecha > DateTime.Today)
            return BadRequest(new { mensaje = "La fecha de corte no puede ser futura." });

        if (fecha.Year < 1900)
            return BadRequest(new { mensaje = "La fecha de corte está fuera del rango permitido." });

        var resultado = await _evaluador.EvaluarAsync(pacienteId, fecha, ct);
        if (resultado is null)
            return NotFound(new { mensaje = "No se encontró un paciente activo con ese identificador." });

        Response.Headers.CacheControl = "no-store";
        return Ok(resultado);
    }
}
