namespace SNCEV.Api.Models;

public sealed class DomicilioPacienteDto
{
    public Guid Id { get; init; }
    public Guid PacienteId { get; init; }

    public string? EstadoClave { get; init; }
    public string? EstadoNombre { get; init; }

    public int? MunicipioId { get; init; }
    public string? MunicipioClaveInegi { get; init; }
    public string? MunicipioNombre { get; init; }

    public string? LocalidadClave { get; init; }
    public string? Localidad { get; init; }
    public string? Colonia { get; init; }
    public string? CodigoPostal { get; init; }

    public string? Calle { get; init; }
    public string? NumeroExterior { get; init; }
    public string? NumeroInterior { get; init; }

    public string? Manzana { get; init; }
    public string? Lote { get; init; }

    public string? DireccionTexto { get; init; }

    public double? Latitud { get; init; }
    public double? Longitud { get; init; }

    public string? AgebClave { get; init; }
    public string? Cvegeo { get; init; }
    public string? CveAgeb { get; init; }

    public string? CluesSugerida { get; init; }
    public string? CluesSugeridaNombre { get; init; }
    public string? CluesSugeridaInstitucion { get; init; }

    public string? OrigenGeorreferencia { get; init; }
    public string? PrecisionGeorreferencia { get; init; }

    public bool Vigente { get; init; }

    public DateTime FechaInicio { get; init; }
    public DateTime? FechaFin { get; init; }
    public DateTime FechaRegistro { get; init; }
}