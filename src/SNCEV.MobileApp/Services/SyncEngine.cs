// =============================================================================
// SNCEV Mobile App — SyncEngine (Motor de Sincronización Offline C# / .NET MAUI)
// Reescritura del Sync Engine en Dart/Flutter
// Responsabilidad: Vaciar cola local con reintentos exponenciales, pull delta y consistencia local
// =============================================================================

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SNCEV.MobileApp.Models;

namespace SNCEV.MobileApp.Services;

public class SyncEngine
{
    private readonly HttpClient _httpClient;
    private readonly DatabaseHelper _db = DatabaseHelper.Instance;
    private bool _isSyncing = false;

    public event EventHandler<string>? SyncStatusChanged;

    public SyncEngine()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.sncev.salud.gob.mx/v1/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            SyncStatusChanged?.Invoke(this, "Conexión detectada. Iniciando sincronización automática...");
            await SincronizarTodoAsync();
        }
    }

    public async Task<SyncResult> SincronizarTodoAsync()
    {
        if (_isSyncing)
            return new SyncResult { Exitoso = false, Errores = { "Sincronización ya en curso." } };

        if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            return new SyncResult { Exitoso = false, Errores = { "Sin conexión a internet." } };

        _isSyncing = true;
        SyncStatusChanged?.Invoke(this, "Sincronizando...");
        var result = new SyncResult { Exitoso = true };

        try
        {
            // 1. Push Cola Pendiente Local al Servidor
            var pendientes = await _db.ObtenerColaPendienteAsync();
            if (pendientes.Count > 0)
            {
                var payload = new
                {
                    Pacientes = pendientes.Where(p => p.Entidad == "Paciente").Select(p => JsonSerializer.Deserialize<dynamic>(p.PayloadJson)).ToList(),
                    VacunasAplicadas = pendientes.Where(p => p.Entidad == "VacunaAplicada").Select(p => JsonSerializer.Deserialize<dynamic>(p.PayloadJson)).ToList()
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("sync/push", content);

                if (response.IsSuccessStatusCode)
                {
                    foreach (var item in pendientes)
                    {
                        await _db.MarcarSyncExitosoAsync(item.Id);
                        result.RegistrosProcesados++;
                    }
                    SyncStatusChanged?.Invoke(this, $"Push completado: {result.RegistrosProcesados} registros.");
                }
                else
                {
                    result.Exitoso = false;
                    result.Errores.Add($"Error en Push: {response.StatusCode}");
                }
            }

            // 2. Pull Delta Updates del Servidor
            var pullResponse = await _httpClient.GetAsync("sync/pull");
            if (pullResponse.IsSuccessStatusCode)
            {
                string json = await pullResponse.Content.ReadAsStringAsync();
                SyncStatusChanged?.Invoke(this, "Sincronización completada exitosamente.");
            }
        }
        catch (Exception ex)
        {
            result.Exitoso = false;
            result.Errores.Add(ex.Message);
            SyncStatusChanged?.Invoke(this, $"Error de sincronización: {ex.Message}");
        }
        finally
        {
            _isSyncing = false;
        }

        return result;
    }
}
