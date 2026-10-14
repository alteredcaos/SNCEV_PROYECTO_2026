using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SNCEV.Api.Services.Geografia;

namespace SNCEV.Api.Controllers;

/// <summary>
/// Completa exclusivamente entidad y municipio del domicilio vigente.
/// No crea episodios, no cambia coordenadas ni infiere AGEB/CLUES.
/// </summary>
[ApiController]
[Route("v1/pacientes/{pacienteId:guid}/domicilios")]
[Authorize(Roles = "Administrador,Capturador")]
public sealed class DomicilioGeografiaController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IGeografiaService _geografia;

    public DomicilioGeografiaController(IConfiguration configuration, IGeografiaService geografia)
    {
        _configuration = configuration;
        _geografia = geografia;
    }

    [HttpPost("vigente/completar-geografia")]
    public async Task<IActionResult> CompletarGeografia(Guid pacienteId, CancellationToken cancellationToken)
    {
        var cadena = _configuration.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException("Falta SqlServerConnection.");

        await using var connection = new SqlConnection(cadena);
        await connection.OpenAsync(cancellationToken);

        const string consultar = """
            SELECT TOP (1) d.id AS Id,
                   CAST(d.latitud AS float) AS Latitud,
                   CAST(d.longitud AS float) AS Longitud,
                   d.estado_clave AS EstadoClave,
                   d.municipio_id AS MunicipioId
            FROM sncev.domicilio_paciente d
            INNER JOIN sncev.paciente p ON p.id = d.paciente_id AND p.eliminado = 0
            WHERE d.paciente_id = @PacienteId AND d.vigente = 1
            ORDER BY d.fecha_inicio DESC;
            """;

        var domicilio = await connection.QuerySingleOrDefaultAsync<DomicilioActual>(
            new CommandDefinition(consultar, new { PacienteId = pacienteId },
                cancellationToken: cancellationToken));

        if (domicilio is null)
            return NotFound(new { message = "No existe domicilio vigente de paciente activo." });

        if (domicilio.EstadoClave is not null && domicilio.MunicipioId is not null)
            return Ok(new { actualizado = false, message = "El domicilio ya tiene entidad y municipio; no se sobrescribieron." });

        if (domicilio.Latitud is null || domicilio.Longitud is null ||
            !double.IsFinite(domicilio.Latitud.Value) || !double.IsFinite(domicilio.Longitud.Value) ||
            domicilio.Latitud is < -90 or > 90 || domicilio.Longitud is < -180 or > 180 ||
            (domicilio.Latitud == 0 && domicilio.Longitud == 0))
            return UnprocessableEntity(new { message = "El domicilio no tiene coordenadas válidas." });

        var geo = await _geografia.ResolverAsync(
            domicilio.Latitud.Value, domicilio.Longitud.Value, 5, cancellationToken);

        if (geo.Entidad is null || geo.Municipio is null ||
            string.IsNullOrWhiteSpace(geo.Entidad.Clave) ||
            string.IsNullOrWhiteSpace(geo.Municipio.Clave))
            return UnprocessableEntity(new { message = "No se pudo validar entidad y municipio con la cartografía y el catálogo.", geografia = geo });

        var claveMunicipal = geo.Entidad.Clave + geo.Municipio.Clave;

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            const string actualizar = """
                UPDATE d
                SET d.estado_clave = @EstadoClave,
                    d.municipio_id = m.id
                FROM sncev.domicilio_paciente d
                INNER JOIN sncev_geo.municipio m ON m.clave_inegi = @ClaveMunicipal
                INNER JOIN sncev_geo.estado e ON e.clave_inegi = @EstadoClave
                WHERE d.id = @DomicilioId
                  AND d.paciente_id = @PacienteId
                  AND d.vigente = 1
                  AND d.estado_clave IS NULL
                  AND d.municipio_id IS NULL
                  AND d.latitud = @Latitud
                  AND d.longitud = @Longitud
                  AND EXISTS (SELECT 1 FROM sncev.paciente p
                              WHERE p.id = d.paciente_id AND p.eliminado = 0);
                """;

            var filas = await connection.ExecuteAsync(new CommandDefinition(
                actualizar,
                new
                {
                    DomicilioId = domicilio.Id,
                    PacienteId = pacienteId,
                    EstadoClave = geo.Entidad.Clave,
                    ClaveMunicipal = claveMunicipal,
                    Latitud = domicilio.Latitud.Value,
                    Longitud = domicilio.Longitud.Value
                }, transaction, cancellationToken: cancellationToken));

            if (filas != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict(new { message = "No se actualizó: catálogo incompleto, datos ya existentes o domicilio modificado concurrentemente." });
            }

            await transaction.CommitAsync(cancellationToken);
            return Ok(new
            {
                actualizado = true,
                domicilioId = domicilio.Id,
                entidad = geo.Entidad,
                municipio = geo.Municipio,
                agebResuelta = geo.AgebResuelta,
                message = "Entidad y municipio completados; AGEB y CLUES no fueron modificadas."
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private sealed class DomicilioActual
    {
        public Guid Id { get; init; }
        public double? Latitud { get; init; }
        public double? Longitud { get; init; }
        public string? EstadoClave { get; init; }
        public int? MunicipioId { get; init; }
    }
}
