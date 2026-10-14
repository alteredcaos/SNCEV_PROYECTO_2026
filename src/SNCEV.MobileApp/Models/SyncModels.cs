// =============================================================================
// SNCEV Mobile App — Modelos de Entidades y Cola de Sincronización Offline (SQLite)
// =============================================================================

using SQLite;

namespace SNCEV.MobileApp.Models;

public enum SyncStatus
{
    Pendiente,
    Sincronizado,
    Error,
    Conflicto
}

[Table("pacientes_local")]
public class PacienteLocal
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Curp { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string PrimerApellido { get; set; } = string.Empty;
    public string SegundoApellido { get; set; } = string.Empty;
    public DateTime FechaNacimiento { get; set; }
    public string Sexo { get; set; } = "M";
    public string EntidadNacimientoClave { get; set; } = string.Empty;
    public int MunicipioResidenciaId { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public bool Sincronizado { get; set; } = false;
}

[Table("vacunas_aplicadas_local")]
public class VacunaAplicadaLocal
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PacienteId { get; set; }
    public int VacunaId { get; set; }
    public string VacunaNombre { get; set; } = string.Empty;
    public int NumeroDosis { get; set; } = 1;
    public DateTime FechaAplicacion { get; set; } = DateTime.Today;
    public string Lote { get; set; } = string.Empty;
    public string Laboratorio { get; set; } = string.Empty;
    public string UnidadMedicaClues { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public string FirmaDigitalKms { get; set; } = string.Empty;
    public bool Sincronizado { get; set; } = false;
}

[Table("cola_sincronizacion")]
public class ColaSincronizacion
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Entidad { get; set; } = string.Empty; // Paciente, VacunaAplicada, Somatometria
    public Guid EntidadId { get; set; }
    public string Operacion { get; set; } = "INSERT"; // INSERT, UPDATE
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int Reintentos { get; set; } = 0;
    public string Estado { get; set; } = "Pendiente";
    public string MensajeError { get; set; } = string.Empty;
}

public class SyncResult
{
    public bool Exitoso { get; set; }
    public int RegistrosProcesados { get; set; }
    public List<string> Errores { get; set; } = new();
}
