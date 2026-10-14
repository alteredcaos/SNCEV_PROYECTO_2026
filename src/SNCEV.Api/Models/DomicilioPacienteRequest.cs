using System.ComponentModel.DataAnnotations;

namespace SNCEV.Api.Models;

public sealed class DomicilioPacienteRequest
{
    [StringLength(2)]
    public string? EstadoClave { get; init; }

    public int? MunicipioId { get; init; }

    [StringLength(10)]
    public string? LocalidadClave { get; init; }

    [StringLength(150)]
    public string? Localidad { get; init; }

    [StringLength(150)]
    public string? Colonia { get; init; }

    [StringLength(10)]
    public string? CodigoPostal { get; init; }

    [StringLength(200)]
    public string? Calle { get; init; }

    [StringLength(30)]
    public string? NumeroExterior { get; init; }

    [StringLength(30)]
    public string? NumeroInterior { get; init; }

    [StringLength(30)]
    public string? Manzana { get; init; }

    [StringLength(30)]
    public string? Lote { get; init; }

    [StringLength(500)]
    public string? DireccionTexto { get; init; }

    [Range(-90, 90)]
    public double Latitud { get; init; }

    [Range(-180, 180)]
    public double Longitud { get; init; }

    [StringLength(30)]
    public string OrigenGeorreferencia { get; init; } = "MAPA";

    [StringLength(30)]
    public string PrecisionGeorreferencia { get; init; } = "MANUAL";
}