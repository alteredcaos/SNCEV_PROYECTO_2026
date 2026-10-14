using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SNCEV.Api.Models;
using SNCEV.Api.Services.Geografia;

namespace SNCEV.Api.Controllers;

[ApiController]
[Route("v1/pacientes/{pacienteId:guid}/domicilios")]
[Authorize]
public sealed class DomiciliosController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IGeografiaService _geografia;

    public DomiciliosController(
        IConfiguration configuration,
        IGeografiaService geografia)
    {
        _configuration = configuration;
        _geografia = geografia;
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Capturador")]
    public async Task<IActionResult> Crear(
        Guid pacienteId,
        [FromBody] DomicilioPacienteRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Validaciones básicas

        if (!double.IsFinite(request.Latitud) ||
            !double.IsFinite(request.Longitud) ||
            request.Latitud is < -90 or > 90 ||
            request.Longitud is < -180 or > 180 ||
            (request.Latitud == 0 && request.Longitud == 0))
        {
            return BadRequest(new
            {
                message = "Debe proporcionar coordenadas válidas del domicilio."
            });
        }

        var origen = request.OrigenGeorreferencia
            .Trim()
            .ToUpperInvariant();

        var precision = request.PrecisionGeorreferencia
            .Trim()
            .ToUpperInvariant();

        if (origen is not ("MAPA" or "GPS" or "DIRECCION"))
        {
            return BadRequest(new
            {
                message = "OrigenGeorreferencia no válido."
            });
        }

        if (precision is not ("EXACTA" or "APROXIMADA" or "MANUAL"))
        {
            return BadRequest(new
            {
                message = "PrecisionGeorreferencia no válida."
            });
        }

        // 2. Resolver geografía en servidor.
        // El frontend NO decide AGEB/CVEGEO/CLUES.

        var geo = await _geografia.ResolverAsync(
            request.Latitud,
            request.Longitud,
            5,
            cancellationToken);

        // La ausencia de polígono AGEB NO impide registrar el domicilio.
        // Nunca se fabrican claves geográficas para suplir cobertura faltante.

        // Por ahora elegimos la unidad candidata más cercana
        // únicamente como SUGERENCIA, no como adscripción.

        var cluesSugerida = geo.CluesCercanas
            .OrderBy(x => x.DistanciaKm)
            .FirstOrDefault();

        var connectionString =
            _configuration.GetConnectionString("SqlServerConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena de conexión configurada.");
        }

        await using var connection =
            new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var transaction =
            (SqlTransaction)await connection.BeginTransactionAsync(
                cancellationToken);

        try
        {
            // 3. Confirmar que el paciente existe y está activo.

            const string sqlPaciente = """
SELECT COUNT(1)
FROM sncev.paciente
WHERE id = @PacienteId
  AND eliminado = 0;
""";

            var existe = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    sqlPaciente,
                    new { PacienteId = pacienteId },
                    transaction,
                    cancellationToken: cancellationToken));

            if (existe == 0)
            {
                await transaction.RollbackAsync(cancellationToken);

                return NotFound(new
                {
                    message =
                        "No se encontró un paciente activo con ese identificador."
                });
            }
            // Comprobar si el domicilio vigente ya coincide con el solicitado.
            const string sqlDomicilioActual = """
SELECT TOP (1)
    direccion_texto AS DireccionTexto,
    latitud AS Latitud,
    longitud AS Longitud,
    localidad AS Localidad,
    colonia AS Colonia,
    codigo_postal AS CodigoPostal,
    calle AS Calle,
    numero_exterior AS NumeroExterior,
    numero_interior AS NumeroInterior,
    localidad_clave AS LocalidadClave,
    manzana AS Manzana,
    lote AS Lote,
    origen_georreferencia AS OrigenGeorreferencia,
    precision_georreferencia AS PrecisionGeorreferencia
FROM sncev.domicilio_paciente
WHERE paciente_id = @PacienteId
  AND vigente = 1
ORDER BY fecha_inicio DESC;
""";

            var actual = await connection.QuerySingleOrDefaultAsync<DomicilioActualSql>(
                new CommandDefinition(
                    sqlDomicilioActual,
                    new { PacienteId = pacienteId },
                    transaction,
                    cancellationToken: cancellationToken));

            if (actual is not null)
            {
                var mismasCoordenadas =
    actual.Latitud.HasValue &&
    actual.Longitud.HasValue &&
    Math.Abs(actual.Latitud.Value - (decimal)request.Latitud) < 0.000001m &&
    Math.Abs(actual.Longitud.Value - (decimal)request.Longitud) < 0.000001m;

                var mismosDatos =
                    Iguales(actual.DireccionTexto, request.DireccionTexto) &&
                    Iguales(actual.Localidad, request.Localidad) &&
                    Iguales(actual.Colonia, request.Colonia) &&
                    Iguales(actual.CodigoPostal, request.CodigoPostal) &&
                    Iguales(actual.Calle, request.Calle) &&
                    Iguales(actual.NumeroExterior, request.NumeroExterior) &&
                    Iguales(actual.NumeroInterior, request.NumeroInterior) &&
                    Iguales(actual.LocalidadClave, request.LocalidadClave) &&
                    Iguales(actual.Manzana, request.Manzana) &&
                    Iguales(actual.Lote, request.Lote) &&
                    Iguales(actual.OrigenGeorreferencia, origen) &&
                    Iguales(actual.PrecisionGeorreferencia, precision);

                if (mismasCoordenadas && mismosDatos)
                {
                    await transaction.RollbackAsync(cancellationToken);

                    return Ok(new
                    {
                        message = "El domicilio proporcionado coincide con el domicilio vigente.",
                        sinCambios = true
                    });
                }
            }

            // 4. Obtener el ID interno del municipio determinado
            //    por las coordenadas/AGEB.

            const string sqlMunicipio = """
SELECT TOP (1)
    m.id
FROM sncev_geo.municipio m
WHERE m.clave_inegi = @ClaveInegi
  AND EXISTS (SELECT 1 FROM sncev_geo.estado e
              WHERE e.clave_inegi = @EstadoClave);
""";

            // Resolver entidad y municipio independientemente de que exista AGEB.
            string? estadoClave = geo.Entidad?.Clave;
            string? municipioClave = geo.Municipio?.Clave;
            int? municipioId = null;

            if (!string.IsNullOrWhiteSpace(estadoClave) &&
                !string.IsNullOrWhiteSpace(municipioClave))
            {
                municipioId = await connection.QuerySingleOrDefaultAsync<int?>(
                    new CommandDefinition(
                        sqlMunicipio,
                        new { ClaveInegi = estadoClave + municipioClave, EstadoClave = estadoClave },
                        transaction,
                        cancellationToken: cancellationToken));
            }

            // Evitar guardar una entidad aislada sin municipio validado.
            // Sin cobertura completa, se conservan ambos campos en NULL.
            if (!municipioId.HasValue)
                estadoClave = null;

            // 5. Cerrar domicilio actualmente vigente.

            const string sqlCerrarAnterior = """
UPDATE sncev.domicilio_paciente
SET
    vigente = 0,
    fecha_fin = SYSDATETIME()
WHERE paciente_id = @PacienteId
  AND vigente = 1;
""";

            await connection.ExecuteAsync(
                new CommandDefinition(
                    sqlCerrarAnterior,
                    new { PacienteId = pacienteId },
                    transaction,
                    cancellationToken: cancellationToken));

            // 6. Insertar nuevo episodio residencial.

            var domicilioId = Guid.NewGuid();

            const string sqlInsertar = """
INSERT INTO sncev.domicilio_paciente
(
    id,
    paciente_id,
    estado_clave,
    municipio_id,
    localidad_clave,
    localidad,
    colonia,
    codigo_postal,
    calle,
    numero_exterior,
    numero_interior,
    manzana,
    lote,
    direccion_texto,
    latitud,
    longitud,
    ageb_clave,
    cvegeo,
    cve_ageb,
    clues_sugerida,
    origen_georreferencia,
    precision_georreferencia,
    vigente,
    fecha_inicio,
    fecha_fin,
    fecha_registro
)
VALUES
(
    @Id,
    @PacienteId,
    @EstadoClave,
    @MunicipioId,
    @LocalidadClave,
    @Localidad,
    @Colonia,
    @CodigoPostal,
    @Calle,
    @NumeroExterior,
    @NumeroInterior,
    @Manzana,
    @Lote,
    @DireccionTexto,
    @Latitud,
    @Longitud,
    @AgebClave,
    @Cvegeo,
    @CveAgeb,
    @CluesSugerida,
    @OrigenGeorreferencia,
    @PrecisionGeorreferencia,
    1,
    SYSDATETIME(),
    NULL,
    SYSDATETIME()
);
""";

            await connection.ExecuteAsync(
                new CommandDefinition(
                    sqlInsertar,
                  new
                  {
                      Id = domicilioId,
                      PacienteId = pacienteId,

                      // Calculados por coordenadas
                      EstadoClave = estadoClave,
                      MunicipioId = municipioId,

                      // Capturados
                      request.LocalidadClave,
                      request.Localidad,
                      request.Colonia,
                      request.CodigoPostal,
                      request.Calle,
                      request.NumeroExterior,
                      request.NumeroInterior,
                      request.Manzana,
                      request.Lote,
                      request.DireccionTexto,

                      request.Latitud,
                      request.Longitud,

                      // Calculados por coordenadas
                      AgebClave = geo.Ageb?.Cvegeo,
                      Cvegeo = geo.Ageb?.Cvegeo,
                      CveAgeb = geo.Ageb?.CveAgeb,

                      // Sólo sugerencia
                      CluesSugerida = cluesSugerida?.Clues,

                      OrigenGeorreferencia = origen,
                      PrecisionGeorreferencia = precision
                  },
                    transaction,
                    cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            // 6. Respuesta

            return Created(
                $"/v1/pacientes/{pacienteId}/domicilios/{domicilioId}",
                new
                {
                    id = domicilioId,
                    pacienteId,

                    domicilio = new
                    {
                        EstadoClave = estadoClave,
                        MunicipioId = municipioId,
                        request.LocalidadClave,
                        request.Localidad,
                        request.Colonia,
                        request.CodigoPostal,
                        request.Calle,
                        request.NumeroExterior,
                        request.NumeroInterior,
                        request.Manzana,
                        request.Lote,
                        request.DireccionTexto,
                        request.Latitud,
                        request.Longitud
                    },

                    geografia = new
                    {
                        geo.Entidad,
                        geo.Municipio,
                        geo.Ageb
                    },

                    cluesSugerida,
                    clasificacionGeograficaPendiente = geo.Ageb is null || municipioId is null,

                    vigente = true
                });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = "Administrador,Capturador,Directivo,Auditor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerHistorial(
    Guid pacienteId,
    CancellationToken cancellationToken)
    {
        var connectionString =
            _configuration.GetConnectionString("SqlServerConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena de conexión configurada.");
        }

        await using var connection =
            new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        // Comprobamos que el paciente exista.
        // No exigimos eliminado = 0 porque Administrador/Auditor
        // pueden necesitar consultar información histórica.

        const string sqlPaciente = """
SELECT COUNT(1)
FROM sncev.paciente
WHERE id = @PacienteId;
""";

        var existe = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sqlPaciente,
                new { PacienteId = pacienteId },
                cancellationToken: cancellationToken));

        if (existe == 0)
        {
            return NotFound(new
            {
                message = "No se encontró el paciente."
            });
        }

        const string sql = """
SELECT
    d.id                            AS Id,
    d.paciente_id                   AS PacienteId,

    d.estado_clave                  AS EstadoClave,
    e.nombre                        AS EstadoNombre,

    d.municipio_id                  AS MunicipioId,
    m.clave_inegi                   AS MunicipioClaveInegi,
    m.nombre                        AS MunicipioNombre,

    d.localidad_clave               AS LocalidadClave,
    d.localidad                     AS Localidad,
    d.colonia                       AS Colonia,
    d.codigo_postal                 AS CodigoPostal,

    d.calle                         AS Calle,
    d.numero_exterior               AS NumeroExterior,
    d.numero_interior               AS NumeroInterior,

    d.manzana                       AS Manzana,
    d.lote                          AS Lote,

    d.direccion_texto               AS DireccionTexto,

    CAST(d.latitud AS float)        AS Latitud,
    CAST(d.longitud AS float)       AS Longitud,

    d.ageb_clave                    AS AgebClave,
    d.cvegeo                        AS Cvegeo,
    d.cve_ageb                      AS CveAgeb,

    d.clues_sugerida                AS CluesSugerida,
    es.nombre_unidad                AS CluesSugeridaNombre,
    es.nombre_institucion           AS CluesSugeridaInstitucion,

    d.origen_georreferencia         AS OrigenGeorreferencia,
    d.precision_georreferencia      AS PrecisionGeorreferencia,

    d.vigente                       AS Vigente,
    d.fecha_inicio                  AS FechaInicio,
    d.fecha_fin                     AS FechaFin,
    d.fecha_registro                AS FechaRegistro

FROM sncev.domicilio_paciente d

LEFT JOIN sncev_geo.estado e
    ON e.clave_inegi = d.estado_clave

LEFT JOIN sncev_geo.municipio m
    ON m.id = d.municipio_id

LEFT JOIN sncev_geo.establecimiento_salud es
    ON es.clues = d.clues_sugerida

WHERE d.paciente_id = @PacienteId

ORDER BY
    d.vigente DESC,
    d.fecha_inicio DESC;
""";

        var domicilios =
            (await connection.QueryAsync<DomicilioPacienteDto>(
                new CommandDefinition(
                    sql,
                    new { PacienteId = pacienteId },
                    cancellationToken: cancellationToken)))
            .ToArray();

        return Ok(new
        {
            pacienteId,
            total = domicilios.Length,
            domicilioVigente = domicilios.FirstOrDefault(x => x.Vigente),
            historial = domicilios
        });
    }
    [HttpGet("vigente")]
    [Authorize(Roles = "Administrador,Capturador,Directivo,Auditor")]
    [ProducesResponseType(typeof(DomicilioPacienteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerVigente(
    Guid pacienteId,
    CancellationToken cancellationToken)
    {
        var connectionString =
            _configuration.GetConnectionString("SqlServerConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena de conexión configurada.");
        }

        await using var connection =
            new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        const string sql = """
SELECT TOP (1)
    d.id                            AS Id,
    d.paciente_id                   AS PacienteId,

    d.estado_clave                  AS EstadoClave,
    e.nombre                        AS EstadoNombre,

    d.municipio_id                  AS MunicipioId,
    m.clave_inegi                   AS MunicipioClaveInegi,
    m.nombre                        AS MunicipioNombre,

    d.localidad_clave               AS LocalidadClave,
    d.localidad                     AS Localidad,
    d.colonia                       AS Colonia,
    d.codigo_postal                 AS CodigoPostal,

    d.calle                         AS Calle,
    d.numero_exterior               AS NumeroExterior,
    d.numero_interior               AS NumeroInterior,

    d.manzana                       AS Manzana,
    d.lote                          AS Lote,

    d.direccion_texto               AS DireccionTexto,

    CAST(d.latitud AS float)        AS Latitud,
    CAST(d.longitud AS float)       AS Longitud,

    d.ageb_clave                    AS AgebClave,
    d.cvegeo                        AS Cvegeo,
    d.cve_ageb                      AS CveAgeb,

    d.clues_sugerida                AS CluesSugerida,
    es.nombre_unidad                AS CluesSugeridaNombre,
    es.nombre_institucion           AS CluesSugeridaInstitucion,

    d.origen_georreferencia         AS OrigenGeorreferencia,
    d.precision_georreferencia      AS PrecisionGeorreferencia,

    d.vigente                       AS Vigente,
    d.fecha_inicio                  AS FechaInicio,
    d.fecha_fin                     AS FechaFin,
    d.fecha_registro                AS FechaRegistro

FROM sncev.domicilio_paciente d

LEFT JOIN sncev_geo.estado e
    ON e.clave_inegi = d.estado_clave

LEFT JOIN sncev_geo.municipio m
    ON m.id = d.municipio_id

LEFT JOIN sncev_geo.establecimiento_salud es
    ON es.clues = d.clues_sugerida

WHERE d.paciente_id = @PacienteId
  AND d.vigente = 1

ORDER BY d.fecha_inicio DESC;
""";

        var domicilio =
            await connection.QuerySingleOrDefaultAsync<DomicilioPacienteDto>(
                new CommandDefinition(
                    sql,
                    new { PacienteId = pacienteId },
                    cancellationToken: cancellationToken));

        if (domicilio is null)
        {
            return NotFound(new
            {
                message = "El paciente no tiene un domicilio vigente registrado."
            });
        }

        return Ok(domicilio);
    }
    private static bool Iguales(string? a, string? b)
    {
        return string.Equals(
            a?.Trim(),
            b?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DomicilioActualSql
    {
        public string? DireccionTexto { get; init; }
        public decimal? Latitud { get; init; }
        public decimal? Longitud { get; init; }

        public string? Localidad { get; init; }
        public string? Colonia { get; init; }
        public string? CodigoPostal { get; init; }
        public string? Calle { get; init; }
        public string? NumeroExterior { get; init; }
        public string? NumeroInterior { get; init; }
        public string? LocalidadClave { get; init; }
        public string? Manzana { get; init; }
        public string? Lote { get; init; }
        public string? OrigenGeorreferencia { get; init; }
        public string? PrecisionGeorreferencia { get; init; }
    }
}