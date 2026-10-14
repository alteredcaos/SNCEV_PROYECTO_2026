using System.ComponentModel.DataAnnotations;

namespace SNCEV.Api.Models;

public sealed class AnularVacunacionRequest
{
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Motivo { get; set; } = string.Empty;
}