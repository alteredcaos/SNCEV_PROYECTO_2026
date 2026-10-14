
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SNCEV.MobileApp.Models.Expediente;

public sealed class ExpedientePacienteResponse
{
    public PacienteExpediente Paciente { get; set; } = new();

    public JsonElement? DomicilioVigente { get; set; }

    public List<VacunaAplicadaExpediente> VacunasAplicadas
    { get; set; } = new();

    public List<JsonElement> Somatometria
    { get; set; } = new();
}

public sealed class PacienteExpediente
{
    public Guid Id { get; set; }
    public string? Curp { get; set; }
    public string Nombre { get; set; } = "";

    [JsonPropertyName("primer_apellido")]
    public string PrimerApellido { get; set; } = "";

    [JsonPropertyName("segundo_apellido")]
    public string? SegundoApellido { get; set; }

    [JsonPropertyName("fecha_nacimiento")]
    public DateTime FechaNacimiento { get; set; }

    public string Sexo { get; set; } = "";

    [JsonPropertyName("entidad_nacimiento")]
    public string? EntidadNacimiento { get; set; }

    [JsonPropertyName("municipio_residencia")]
    public string? MunicipioResidencia { get; set; }

    [JsonPropertyName("estado_residencia")]
    public string? EstadoResidencia { get; set; }

    public string? Localidad { get; set; }

    [JsonPropertyName("tipo_cartilla")]
    public string? TipoCartilla { get; set; }

    [JsonIgnore]
    public string NombreCompleto =>
        $"{Nombre} {PrimerApellido} {SegundoApellido}".Trim();

    [JsonIgnore]
    public string FechaNacimientoTexto =>
        FechaNacimiento.ToString("dd/MM/yyyy");



    public string? Direccion { get; set; }

    [JsonPropertyName("ageb_clave")]
    public string? AgebClave { get; set; }

   
}

public sealed class VacunaAplicadaExpediente
{
    public Guid Id { get; set; }

    [JsonPropertyName("vacuna_nombre")]
    public string VacunaNombre { get; set; } = "";

    [JsonPropertyName("numero_dosis")]
    public int NumeroDosis { get; set; }

    [JsonPropertyName("fecha_aplicacion")]
    public DateTime FechaAplicacion { get; set; }

    public string? Lote { get; set; }
    public string? Laboratorio { get; set; }
    public string? Observaciones { get; set; }
    public bool Anulada { get; set; }

    [JsonPropertyName("unidad_medica_clues")]
    public string? UnidadMedicaClues { get; set; }

    [JsonIgnore]
    public string Descripcion =>
        $"Dosis {NumeroDosis} · {FechaAplicacion:dd/MM/yyyy}";

    [JsonIgnore]
    public string EstadoTexto =>
        Anulada ? "ANULADA" : "Registrada";
}
