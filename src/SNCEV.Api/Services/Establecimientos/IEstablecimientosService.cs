namespace SNCEV.Api.Services.Establecimientos;

public sealed class EstablecimientoDto
{
    public string Clues { get; set; } = "";
    public string NombreUnidad { get; set; } = "";
    public string? Municipio { get; set; }
    public string? Jurisdiccion { get; set; }
    public string? Institucion { get; set; }
    public string? EstatusOperacion { get; set; }
}

public interface IEstablecimientosService
{
    Task<IReadOnlyList<EstablecimientoDto>> BuscarAsync(string? buscar, int limite, CancellationToken ct);
    Task<EstablecimientoDto?> ObtenerAsync(string clues, CancellationToken ct);
    Task<IReadOnlyList<EstablecimientoDto>> CatalogoAsync(CancellationToken ct);
}
