// =============================================================================
// SNCEV Mobile App — DatabaseHelper (SQLite Offline Engine)
// =============================================================================

using SQLite;
using SNCEV.MobileApp.Models;

namespace SNCEV.MobileApp.Services;

public class DatabaseHelper
{
    private static DatabaseHelper? _instance;
    public static DatabaseHelper Instance => _instance ??= new DatabaseHelper();

    private readonly SQLiteAsyncConnection _db;

    private DatabaseHelper()
    {
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "sncev_offline.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<PacienteLocal>().Wait();
        _db.CreateTableAsync<VacunaAplicadaLocal>().Wait();
        _db.CreateTableAsync<ColaSincronizacion>().Wait();
        _db.CreateTableAsync<EstablecimientoLocal>().Wait();
    }

    public Task<List<PacienteLocal>> ObtenerPacientesAsync(string filtro = "")
    {
        if (string.IsNullOrWhiteSpace(filtro))
            return _db.Table<PacienteLocal>().ToListAsync();

        return _db.Table<PacienteLocal>()
            .Where(p => p.Curp.Contains(filtro) || p.Nombre.Contains(filtro) || p.PrimerApellido.Contains(filtro))
            .ToListAsync();
    }

    public Task<PacienteLocal?> ObtenerPacientePorIdAsync(Guid id)
    {
        return _db.Table<PacienteLocal>().Where(p => p.Id == id).FirstOrDefaultAsync();
    }

    public async Task InsertarPacienteAsync(PacienteLocal paciente)
    {
        await _db.InsertAsync(paciente);
        await _db.InsertAsync(new ColaSincronizacion
        {
            Entidad = "Paciente",
            EntidadId = paciente.Id,
            Operacion = "INSERT",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(paciente)
        });
    }

    public Task<List<VacunaAplicadaLocal>> ObtenerVacunasDePacienteAsync(Guid pacienteId)
    {
        return _db.Table<VacunaAplicadaLocal>().Where(v => v.PacienteId == pacienteId).ToListAsync();
    }

    public async Task RegistrarDosisAsync(VacunaAplicadaLocal dosis)
    {
        await _db.InsertAsync(dosis);
        await _db.InsertAsync(new ColaSincronizacion
        {
            Entidad = "VacunaAplicada",
            EntidadId = dosis.Id,
            Operacion = "INSERT",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(dosis)
        });
    }

    public Task<List<ColaSincronizacion>> ObtenerColaPendienteAsync()
    {
        return _db.Table<ColaSincronizacion>().Where(c => c.Estado == "Pendiente").ToListAsync();
    }

    public Task MarcarSyncExitosoAsync(int colaId)
    {
        return _db.ExecuteAsync("UPDATE cola_sincronizacion SET Estado = 'Sincronizado' WHERE Id = ?", colaId);
    }

    // Catálogo local independiente de la cola clínica.
    public Task<List<EstablecimientoLocal>> ObtenerEstablecimientosAsync()
        => _db.Table<EstablecimientoLocal>().OrderBy(x => x.NombreUnidad).ToListAsync();

    public Task<EstablecimientoLocal?> ObtenerEstablecimientoAsync(string clues)
        => _db.Table<EstablecimientoLocal>()
            .Where(x => x.Clues == clues).FirstOrDefaultAsync();

    public async Task<int> ReemplazarCatalogoEstablecimientosAsync(
        IReadOnlyCollection<EstablecimientoLocal> establecimientos)
    {
        // Solo llamar tras recibir y validar una descarga completa.
        // Nunca reemplazar el catálogo por un resultado paginado de 30 filas.
        if (establecimientos.Count == 0)
            throw new ArgumentException("No se permite reemplazar el catálogo con una lista vacía.", nameof(establecimientos));

        var unidades = establecimientos.ToArray();
        if (unidades.Any(x => string.IsNullOrWhiteSpace(x.Clues) ||
                             string.IsNullOrWhiteSpace(x.NombreUnidad)) ||
            unidades.Select(x => x.Clues).Distinct(StringComparer.OrdinalIgnoreCase).Count() != unidades.Length)
            throw new ArgumentException("El catálogo contiene claves vacías, nombres vacíos o CLUES duplicadas.", nameof(establecimientos));

        await _db.RunInTransactionAsync(connection =>
        {
            connection.DeleteAll<EstablecimientoLocal>();
            connection.InsertAll(unidades);
        });
        return unidades.Length;
    }

}
