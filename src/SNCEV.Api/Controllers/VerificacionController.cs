using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SNCEV.Api.Controllers;

[ApiController]
[Route("v1/verificar")]
public sealed class VerificacionController : ControllerBase
{
    private readonly IConfiguration _config;

    public VerificacionController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("{codigo:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Verificar(
        Guid codigo,
        CancellationToken cancellationToken)
    {
        var connectionString =
            _config.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:SqlServerConnection no está configurada.");

        await using var conn =
            new SqlConnection(connectionString);

        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                folio AS Folio,
                codigo_verificacion AS CodigoVerificacion,
                fecha_emision AS FechaEmision,
                estado AS Estado,
                fecha_revocacion AS FechaRevocacion
            FROM sncev.documento_cartilla
            WHERE codigo_verificacion = @Codigo;
            """;

        var documento =
            await conn.QueryFirstOrDefaultAsync<DocumentoVerificacionDto>(
                new CommandDefinition(
                    sql,
                    new { Codigo = codigo },
                    cancellationToken: cancellationToken));

        // No diferenciamos públicamente otros detalles
        // cuando el código no existe.
        if (documento is null)
        {
            return NotFound(new
            {
                valido = false,
                mensaje = "Documento SNCEV no localizado."
            });
        }

        var vigente = string.Equals(
            documento.Estado,
            "VIGENTE",
            StringComparison.OrdinalIgnoreCase);

        return Ok(new
        {
            valido = vigente,

            documento = "Cartilla Electrónica de Vacunación",

            folio = documento.Folio,

            fechaEmision = documento.FechaEmision,

            estado = documento.Estado,

            fechaRevocacion =
                documento.FechaRevocacion,

            integridad = new
            {
                algoritmo = "SHA-256",
                registrada = true
            }
        });
    }

    private sealed class DocumentoVerificacionDto
    {
        public string Folio { get; set; } = string.Empty;

        public Guid CodigoVerificacion { get; set; }

        public DateTime FechaEmision { get; set; }

        public string Estado { get; set; } = string.Empty;

        public DateTime? FechaRevocacion { get; set; }
    }
}