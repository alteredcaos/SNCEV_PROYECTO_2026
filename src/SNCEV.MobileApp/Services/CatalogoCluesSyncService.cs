using System.Net.Http.Headers;
using System.Net.Http.Json;
using SNCEV.MobileApp.Models;

namespace SNCEV.MobileApp.Services;

public sealed class CatalogoCluesSyncService
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public CatalogoCluesSyncService(HttpClient http) => _http = http;

    // El token no se agrega a DefaultRequestHeaders: HttpClient es compartido.
    public async Task<int> ActualizarAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var token = await SecureStorage.Default.GetAsync("sncev_access_token");
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Inicia sesión para descargar el catálogo CLUES.");

            using var request = new HttpRequestMessage(HttpMethod.Get, "establecimientos/catalogo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<List<EstablecimientoLocal>>(cancellationToken: ct)
                ?? throw new InvalidOperationException("El servidor devolvió un catálogo nulo.");
            if (items.Count == 0)
                throw new InvalidOperationException("El catálogo llegó vacío; se conservará el anterior.");
            // Un catálogo completo, validado y no paginado reemplaza al anterior en una transacción.
            return await DatabaseHelper.Instance.ReemplazarCatalogoEstablecimientosAsync(items);
        }
        finally { _mutex.Release(); }
    }

    public Task<List<EstablecimientoLocal>> ObtenerLocalesAsync()
        => DatabaseHelper.Instance.ObtenerEstablecimientosAsync();
}
