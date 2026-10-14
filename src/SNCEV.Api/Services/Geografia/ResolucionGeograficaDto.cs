namespace SNCEV.Api.Services.Geografia;

public sealed class ResolucionGeograficaDto
{
    public CoordenadaDto Coordenada { get; init; } = new();

    public bool AgebResuelta { get; init; }

    public string? Motivo { get; init; }

    public EntidadGeoDto? Entidad { get; init; }

    public MunicipioGeoDto? Municipio { get; init; }

    public AgebGeoDto? Ageb { get; init; }

    public IReadOnlyList<CluesCercanaDto> CluesCercanas { get; init; }
        = Array.Empty<CluesCercanaDto>();
}

public sealed class CoordenadaDto
{
    public double Latitud { get; init; }
    public double Longitud { get; init; }
}

public sealed class EntidadGeoDto
{
    public string Clave { get; init; } = "";
    public string Nombre { get; init; } = "";
}

public sealed class MunicipioGeoDto
{
    public string Clave { get; init; } = "";
    public string Nombre { get; init; } = "";
}

public sealed class AgebGeoDto
{
    public string Cvegeo { get; init; } = "";
    public string CveAgeb { get; init; } = "";
    public string CveLoc { get; init; } = "";
}

public sealed class CluesCercanaDto
{
    public string Clues { get; init; } = "";
    public string Nombre { get; init; } = "";

    public double Latitud { get; init; }
    public double Longitud { get; init; }

    public double DistanciaKm { get; init; }

    public string? Institucion { get; init; }
    public string? Estatus { get; init; }
}