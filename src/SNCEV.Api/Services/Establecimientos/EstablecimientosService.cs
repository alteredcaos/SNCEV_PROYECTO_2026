using Dapper;
using Microsoft.Data.SqlClient;

namespace SNCEV.Api.Services.Establecimientos;

public sealed class EstablecimientosService : IEstablecimientosService
{
    private readonly string _connectionString;

    public EstablecimientosService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:SqlServerConnection.");
    }

    // Candidato a vacunación es una preselección técnica, no una autorización clínica.
    private const string BaseSql = """
        SELECT es.clues AS Clues,
               es.nombre_unidad AS NombreUnidad,
               es.municipio AS Municipio,
               es.jurisdiccion AS Jurisdiccion,
               es.nombre_institucion AS Institucion,
               es.estatus_operacion AS EstatusOperacion
        FROM sncev_geo.establecimiento_salud es
        INNER JOIN sncev_geo.establecimiento_configuracion cfg
            ON cfg.clues = es.clues AND cfg.candidato_vacunacion = 1
        WHERE es.seleccionable = 1
          AND es.estatus_operacion = 'EN OPERACION'
        """;

    public async Task<IReadOnlyList<EstablecimientoDto>> BuscarAsync(string? buscar, int limite, CancellationToken ct)
    {
        buscar = buscar?.Trim();
        const string suffix = """
          AND (@Buscar IS NULL
               OR es.clues LIKE '%' + @Buscar + '%'
               OR es.nombre_unidad LIKE '%' + @Buscar + '%'
               OR es.municipio LIKE '%' + @Buscar + '%')
          ORDER BY es.nombre_unidad, es.clues
          OFFSET 0 ROWS FETCH NEXT @Limite ROWS ONLY
        """;
        await using var db = new SqlConnection(_connectionString);
        await db.OpenAsync(ct);
        var rows = await db.QueryAsync<EstablecimientoDto>(
            new CommandDefinition(BaseSql + suffix,
                new { Buscar = string.IsNullOrEmpty(buscar) ? null : buscar, Limite = limite },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<EstablecimientoDto>> CatalogoAsync(CancellationToken ct)
    {
        const string order = " ORDER BY es.nombre_unidad, es.clues";
        await using var db = new SqlConnection(_connectionString);
        await db.OpenAsync(ct);
        var rows = await db.QueryAsync<EstablecimientoDto>(
            new CommandDefinition(BaseSql + order, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<EstablecimientoDto?> ObtenerAsync(string clues, CancellationToken ct)
    {
        await using var db = new SqlConnection(_connectionString);
        await db.OpenAsync(ct);
        return await db.QuerySingleOrDefaultAsync<EstablecimientoDto>(
            new CommandDefinition(BaseSql + " AND es.clues = @Clues",
                new { Clues = clues }, cancellationToken: ct));
    }


}
