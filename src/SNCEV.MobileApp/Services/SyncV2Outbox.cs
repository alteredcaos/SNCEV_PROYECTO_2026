using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SQLite;
using SNCEV.MobileApp.Models;

namespace SNCEV.MobileApp.Services;

// Standalone v2 service. Existing SyncEngine and DatabaseHelper remain untouched.
// Use only after testing against the v2 endpoint in a development database.
[Table("sync_v2_outbox")]
public sealed class SyncV2OutboxItem
{
    [PrimaryKey] public string OperacionId { get; set; } = Guid.NewGuid().ToString("D");
    [Indexed] public string Entidad { get; set; } = "";
    public string EntidadLocalId { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    [Indexed] public string Estado { get; set; } = "Pendiente";
    public int Reintentos { get; set; }
    public DateTime ProximoIntentoUtc { get; set; } = DateTime.UtcNow;
    public string? CodigoResultado { get; set; }
    public string? EntidadCentralId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
}

[Table("sync_v2_meta")]
public sealed class SyncV2Meta
{
    [PrimaryKey] public string Clave { get; set; } = "";
    public string Valor { get; set; } = "";
}

public sealed record SyncV2Result(Guid OperacionId, string Estado, Guid? EntidadCentralId, string? Codigo);
public sealed record SyncV2PushResponse(List<SyncV2Result> Resultados);

public sealed class SyncV2Outbox : IDisposable
{
    private readonly SQLiteAsyncConnection _db;
    private readonly HttpClient _http;
    private readonly Func<Task<string?>> _getJwt;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    public SyncV2Outbox(HttpClient http, Func<Task<string?>> getJwt)
    {
        _http = http;
        _getJwt = getJwt;
        var path = Path.Combine(FileSystem.AppDataDirectory, "sncev_offline.db3");
        _db = new SQLiteAsyncConnection(path);
    }

    public async Task InicializarAsync()
    {
        if (_initialized) return;
        await _gate.WaitAsync();
        try
        {
            if (_initialized) return;
            // Las filas nominales y el outbox comparten una base y una transacción.
            await _db.CreateTableAsync<PacienteLocal>();
            await _db.CreateTableAsync<VacunaAplicadaLocal>();
            await _db.CreateTableAsync<SyncV2OutboxItem>();
            await _db.CreateTableAsync<SyncV2Meta>();
            var existing = await _db.FindAsync<SyncV2Meta>("dispositivoId");
            if (existing == null)
                await _db.InsertAsync(new SyncV2Meta { Clave="dispositivoId", Valor=Guid.NewGuid().ToString("D") });
            _initialized = true;
        }
        finally { _gate.Release(); }
    }

    // Persist the domain row and its outbox operation in ONE SQLite transaction.
    // Call this instead of a separate InsertAsync + queue insertion.
    public async Task<Guid> GuardarPacienteAsync(Models.PacienteLocal paciente)
    {
        await InicializarAsync();
        var opId = Guid.NewGuid();
        if (paciente.Id == Guid.Empty) throw new ArgumentException("Paciente.Id obligatorio");
        if (string.IsNullOrWhiteSpace(paciente.EntidadNacimientoClave))
            throw new ArgumentException("EntidadNacimientoClave obligatorio");
        var row = NewItem(opId, "Paciente", paciente.Id, new {
            id = paciente.Id, curp = string.IsNullOrWhiteSpace(paciente.Curp) ? null : paciente.Curp,
            nombre = paciente.Nombre, primerApellido = paciente.PrimerApellido,
            segundoApellido = paciente.SegundoApellido, fechaNacimiento = paciente.FechaNacimiento,
            sexo = paciente.Sexo, entidadNacimientoClave = paciente.EntidadNacimientoClave,
            municipioResidenciaId = paciente.MunicipioResidenciaId
        });
        await _db.RunInTransactionAsync(db =>
        {
            db.Insert(paciente);
            db.Insert(row);
        });
        return opId;
    }

    public async Task<Guid> GuardarDosisAsync(Models.VacunaAplicadaLocal dosis)
    {
        await InicializarAsync();
        var opId = Guid.NewGuid();
        if (dosis.Id == Guid.Empty || dosis.PacienteId == Guid.Empty)
            throw new ArgumentException("Identificadores de dosis y paciente obligatorios");
        var row = NewItem(opId, "VacunaAplicada", dosis.Id, new {
            id = dosis.Id, pacienteId = dosis.PacienteId, vacunaId = dosis.VacunaId,
            numeroDosis = dosis.NumeroDosis, fechaAplicacion = dosis.FechaAplicacion,
            lote = dosis.Lote, laboratorio = dosis.Laboratorio,
            unidadMedicaClues = dosis.UnidadMedicaClues, observaciones = dosis.Observaciones
        });
        await _db.RunInTransactionAsync(db =>
        {
            db.Insert(dosis);
            db.Insert(row);
        });
        return opId;
    }

    // Consulta exclusivamente local; no envía solicitudes HTTP.
    public async Task<List<VacunaAplicadaLocal>> ObtenerDosisPendientesAsync(Guid pacienteId)
    {
        await InicializarAsync();
        var operaciones = await _db.Table<SyncV2OutboxItem>()
            .Where(x => x.Entidad == "VacunaAplicada" && x.Estado == "Pendiente")
            .ToListAsync();
        var ids = operaciones.Select(x => x.EntidadLocalId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dosis = await _db.Table<VacunaAplicadaLocal>()
            .Where(x => x.PacienteId == pacienteId).ToListAsync();
        return dosis.Where(x => ids.Contains(x.Id.ToString("D"))).ToList();
    }

    public async Task<bool> ExisteDosisPendienteAsync(Guid pacienteId, int vacunaId, int numeroDosis)
    {
        var dosis = await ObtenerDosisPendientesAsync(pacienteId);
        return dosis.Any(x => x.VacunaId == vacunaId && x.NumeroDosis == numeroDosis);
    }

    public async Task<int> ContarPendientesAsync()
    {
        await InicializarAsync();
        return await _db.Table<SyncV2OutboxItem>().CountAsync(x => x.Estado == "Pendiente");
    }

    private static SyncV2OutboxItem NewItem<T>(Guid opId, string entity, Guid localId, T value) => new()
    {
        OperacionId=opId.ToString("D"), Entidad=entity,
        EntidadLocalId=localId.ToString("D"), PayloadJson=JsonSerializer.Serialize(value)
    };

    public async Task<int> SincronizarAsync(CancellationToken ct = default)
    {
        await InicializarAsync();
        if (!await _gate.WaitAsync(0, ct)) return 0;
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return 0;
            var jwt = await _getJwt();
            if (string.IsNullOrWhiteSpace(jwt)) return 0;
            var now = DateTime.UtcNow;
            var due = await _db.Table<SyncV2OutboxItem>()
                .Where(x => x.Estado == "Pendiente" && x.ProximoIntentoUtc <= now)
                .OrderBy(x => x.CreadoUtc).Take(50).ToListAsync();
            if (due.Count == 0) return 0;
            var meta = await _db.FindAsync<SyncV2Meta>("dispositivoId");
            var operations = due.OrderBy(x => x.Entidad == "Paciente" ? 0 : 1).Select(x => new
            {
                operacionId = Guid.Parse(x.OperacionId), entidad = x.Entidad,
                entidadLocalId = Guid.Parse(x.EntidadLocalId),
                payload = JsonSerializer.Deserialize<JsonElement>(x.PayloadJson)
            }).ToArray();
            using var request = new HttpRequestMessage(HttpMethod.Post, "sync/v2/push");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            request.Content = JsonContent.Create(new { dispositivoId=Guid.Parse(meta!.Valor), operaciones=operations });
            try
            {
                using var response = await _http.SendAsync(request, ct);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return 0; // Session requires renewal; no records discarded.
                if (!response.IsSuccessStatusCode)
                { await ReprogramarAsync(due); return 0; }
                var body = await response.Content.ReadFromJsonAsync<SyncV2PushResponse>(cancellationToken: ct);
                var byId = (body?.Resultados ?? new()).ToDictionary(x => x.OperacionId.ToString("D"));
                int confirmed = 0;
                foreach (var item in due)
                {
                    if (!byId.TryGetValue(item.OperacionId, out var result))
                    { await ReprogramarAsync(new[] { item }); continue; }
                    if (result.Estado == "Confirmada")
                    {
                        item.Estado = "Confirmada";
                        item.EntidadCentralId = result.EntidadCentralId?.ToString("D");
                        confirmed++;
                    }
                    else if (result.Estado is "Conflicto" or "Rechazada")
                    {
                        item.Estado = result.Estado;
                        item.CodigoResultado = result.Codigo;
                    }
                    else { await ReprogramarAsync(new[] { item }); continue; }
                    await _db.UpdateAsync(item);
                }
                return confirmed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            { await ReprogramarAsync(due); return 0; }
        }
        finally { _gate.Release(); }
    }

    private async Task ReprogramarAsync(IEnumerable<SyncV2OutboxItem> rows)
    {
        foreach (var item in rows)
        {
            item.Reintentos++;
            var seconds = Math.Min(3600, 5 * Math.Pow(2, Math.Min(item.Reintentos, 10)));
            item.ProximoIntentoUtc = DateTime.UtcNow.AddSeconds(seconds);
            await _db.UpdateAsync(item);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.Dispose();
    }
}
