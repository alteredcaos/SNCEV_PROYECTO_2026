namespace SNCEV.Api.Services.Geografia;

public interface IGeografiaService
{
    Task<ResolucionGeograficaDto> ResolverAsync(
        double latitud,
        double longitud,
        int limiteClues = 5,
        CancellationToken cancellationToken = default);
    string? ObtenerAgebGeoJson(string cvegeo);
}