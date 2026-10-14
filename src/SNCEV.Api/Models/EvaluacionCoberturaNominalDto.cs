namespace SNCEV.Api.Models;

public sealed class EvaluacionCoberturaNominalDto
{
    public Guid PacienteId { get; set; }
    public DateTime FechaCorte { get; set; }
    public int EdadAnios { get; set; }
    public string EtapaVida { get; set; } = string.Empty;
    public string Estado { get; set; } = "NO_EVALUABLE";
    public bool EsEvaluable { get; set; }
    public bool? AlCorrienteParaEdad { get; set; }
    public bool EsquemaCompletoEtapa { get; set; }
    public int DosisExigibles { get; set; }
    public int DosisExigiblesAplicadas { get; set; } // Compatibilidad: no inferir cumplimiento
    public int DosisAplicadasDocumentadas { get; set; }
    public int DosisExigiblesActuales { get; set; }
    public int DosisExigiblesActualesAplicadas { get; set; }
    public int DosisPendientes { get; set; }
    public int DosisVencidas { get; set; }
    public int DosisFuturas { get; set; }
    public int DosisRequierenVerificacion { get; set; }
    public int DosisHistoricasNoDocumentadas { get; set; }
    public List<DetalleCoberturaNominalDto> Detalle { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
}

public sealed class DetalleCoberturaNominalDto
{
    public int VacunaId { get; set; }
    public string Vacuna { get; set; } = string.Empty;
    public int NumeroDosis { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string ClasificacionTemporal { get; set; } = string.Empty;
    public DateTime? FechaAplicacion { get; set; }
    public DateTime? FechaMinima { get; set; }
    public DateTime? FechaMaxima { get; set; }
    public string Motivo { get; set; } = string.Empty;
}
