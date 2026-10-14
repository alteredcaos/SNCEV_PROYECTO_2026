
using System.Text.Json.Serialization;

namespace SNCEV.MobileApp.Models;

public sealed class PacienteResumen
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("curp")]
    public string? Curp { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "";

    [JsonPropertyName("primer_apellido")]
    public string PrimerApellido { get; set; } = "";

    [JsonPropertyName("segundo_apellido")]
    public string? SegundoApellido { get; set; }

    [JsonPropertyName("fecha_nacimiento")]
    public DateTime FechaNacimiento { get; set; }

    [JsonPropertyName("sexo")]
    public string Sexo { get; set; } = "";

    [JsonIgnore]
    public string NombreCompleto =>
        $"{Nombre} {PrimerApellido} {SegundoApellido}".Trim();

    [JsonIgnore]
    public string FechaNacimientoTexto =>
        FechaNacimiento.ToString("dd/MM/yyyy");

    [JsonIgnore]
    public string CurpCompleta =>
        string.IsNullOrWhiteSpace(Curp)
            ? "CURP: No disponible"
            : $"CURP: {Curp.Trim().ToUpperInvariant()}";

    [JsonIgnore]
    public string SexoTexto => Sexo?.Trim().ToUpperInvariant() switch
    {
        "M" => "Masculino",
        "F" => "Femenino",
        _ => string.IsNullOrWhiteSpace(Sexo) ? "No registrado" : Sexo
    };

    [JsonIgnore]
    public string CurpProtegida =>
        string.IsNullOrWhiteSpace(Curp)
            ? "No disponible"
            : Curp.Length >= 4
                ? new string('•', Curp.Length - 4) + Curp[^4..]
                : "••••";
}
