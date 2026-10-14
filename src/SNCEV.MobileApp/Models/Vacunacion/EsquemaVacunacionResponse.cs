
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SNCEV.MobileApp.Models.Vacunacion;

public sealed class EsquemaVacunacionResponse
{
    public Guid PacienteId { get; set; }
    public string NombrePaciente { get; set; } = "";
    public DateTime FechaNacimiento { get; set; }
    public string Sexo { get; set; } = "";
    public EdadVacunacion? Edad { get; set; }

    public List<VacunaAplicadaDto> Aplicadas { get; set; } = new();
    public List<VacunaProgramadaDto> Aplicables { get; set; } = new();
    public List<VacunaProgramadaDto> Programadas { get; set; } = new();

    public List<JsonElement> RequierenVerificacion { get; set; } = new();
    public JsonElement? Recomendacion { get; set; }

    public DateTimeOffset FechaEvaluacion { get; set; }
}

public sealed class EdadVacunacion
{
    public int Anios { get; set; }
    public int Meses { get; set; }
    public int Dias { get; set; }
    public int TotalDias { get; set; }
    public string Texto { get; set; } = "";
}

public sealed class VacunaAplicadaDto
{
    public Guid AplicacionId { get; set; }
    public int VacunaId { get; set; }
    public string ClaveAnx { get; set; } = "";
    public string Vacuna { get; set; } = "";
    public int NumeroDosis { get; set; }
    public DateTime FechaAplicacion { get; set; }
    public string? Lote { get; set; }
    public string? Laboratorio { get; set; }
    public string? UnidadMedicaClues { get; set; }
    public string? GrupoPoblacional { get; set; }
    public bool EsEsquemaCompleto { get; set; }

    [JsonIgnore]
    public string Descripcion =>
        $"Dosis {NumeroDosis} · {FechaAplicacion:dd/MM/yyyy}";

    [JsonIgnore]
    public string DatosAplicacion =>
        $"Lote: {Lote ?? "No informado"} · " +
        $"Unidad: {UnidadMedicaClues ?? "No informada"}";
}

public sealed class VacunaProgramadaDto
{
    public int ReglaId { get; set; }
    public int VacunaId { get; set; }
    public string ClaveAnx { get; set; } = "";
    public string Vacuna { get; set; } = "";
    public int NumeroDosis { get; set; }
    public string TipoDosis { get; set; } = "";
    public string Estado { get; set; } = "";
    public string? GrupoPoblacional { get; set; }
    public string? EdadTexto { get; set; }
    public int? EdadMinDias { get; set; }
    public int? EdadMaxDias { get; set; }
    public int? IntervaloDiasMin { get; set; }
    public DateTime? FechaMinimaAplicacion { get; set; }
    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }
    public bool ReglaMarcadaParaVerificacion { get; set; }

    [JsonIgnore]
    public string Descripcion =>
        $"Dosis {NumeroDosis} · {TipoDosis} · {Estado}";

    [JsonIgnore]
    public string FechaTexto =>
        FechaMinimaAplicacion.HasValue
            ? $"Fecha mínima calculada: {FechaMinimaAplicacion:dd/MM/yyyy}"
            : "Fecha mínima no determinada";
}
