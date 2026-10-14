using SQLite;

namespace SNCEV.MobileApp.Models;

[Table("catalogo_establecimientos_local")]
public sealed class EstablecimientoLocal
{
    [PrimaryKey, MaxLength(20)]
    public string Clues { get; set; } = "";
    public string NombreUnidad { get; set; } = "";
    public string Municipio { get; set; } = "";
    public string Jurisdiccion { get; set; } = "";
    public string Institucion { get; set; } = "";
    public string EstatusOperacion { get; set; } = "";
}
