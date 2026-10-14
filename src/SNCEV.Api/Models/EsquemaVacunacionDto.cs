namespace SNCEV.Api.Models;

public sealed class EsquemaVacunacionDto
{
    public Guid PacienteId { get; set; }

    public string NombrePaciente { get; set; } = string.Empty;

    public DateTime FechaNacimiento { get; set; }

    public string Sexo { get; set; } = string.Empty;

    public EdadPacienteDto Edad { get; set; } = new();

    public List<DosisAplicadaDto> Aplicadas { get; set; } = new();

    public List<DosisEsquemaDto> Aplicables { get; set; } = new();

    public List<DosisEsquemaDto> Programadas { get; set; } = new();

    public List<DosisEsquemaDto> RequierenVerificacion { get; set; } = new();

    public RecomendacionVacunacionDto? Recomendacion { get; set; }

    public DateTime FechaEvaluacion { get; set; }
}

public sealed class EdadPacienteDto
{
    public int Anios { get; set; }

    public int Meses { get; set; }

    public int Dias { get; set; }

    public int TotalDias { get; set; }

    public string Texto { get; set; } = string.Empty;
}

public sealed class DosisAplicadaDto
{
    public Guid AplicacionId { get; set; }

    public int VacunaId { get; set; }

    public string ClaveAnx { get; set; } = string.Empty;

    public string Vacuna { get; set; } = string.Empty;

    public int NumeroDosis { get; set; }

    public DateTime FechaAplicacion { get; set; }

    public string Lote { get; set; } = string.Empty;

    public string? Laboratorio { get; set; }

    public string UnidadMedicaClues { get; set; } = string.Empty;

    public string? GrupoPoblacional { get; set; }

    public bool EsEsquemaCompleto { get; set; }
}

public sealed class DosisEsquemaDto
{
    public int ReglaId { get; set; }

    public int VacunaId { get; set; }

    public string ClaveAnx { get; set; } = string.Empty;

    public string Vacuna { get; set; } = string.Empty;

    public int NumeroDosis { get; set; }

    public string TipoDosis { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public string? GrupoPoblacional { get; set; }

    public string? EdadTexto { get; set; }

    public int? EdadMinDias { get; set; }

    public int? EdadMaxDias { get; set; }

    public int? IntervaloDiasMin { get; set; }

    public DateTime? FechaMinimaAplicacion { get; set; }

    public string Motivo { get; set; } = string.Empty;

    public string? Observaciones { get; set; }

    public bool ReglaMarcadaParaVerificacion { get; set; }
}

public sealed class RecomendacionVacunacionDto
{
    public int VacunaId { get; set; }

    public string ClaveAnx { get; set; } = string.Empty;

    public string Vacuna { get; set; } = string.Empty;

    public int NumeroDosis { get; set; }

    public string TipoDosis { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public string Motivo { get; set; } = string.Empty;

    public DateTime? FechaMinimaAplicacion { get; set; }
}