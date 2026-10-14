using System.ComponentModel.DataAnnotations;

namespace SNCEV.Api.Models;

public sealed class RegistrarVacunacionRequest
{
    [Required]
    public int VacunaId { get; set; }

    [Range(0, 20)]
    public int NumeroDosis { get; set; }

    [Required]
    public DateTime FechaAplicacion { get; set; }

    [Required]
    [StringLength(50)]
    public string Lote { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Laboratorio { get; set; }

    [StringLength(255)]
    public string? Observaciones { get; set; }

    [StringLength(150)]
    public string? GrupoPoblacional { get; set; }
}