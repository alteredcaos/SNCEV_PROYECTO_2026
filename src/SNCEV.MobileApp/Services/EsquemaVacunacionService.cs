
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SNCEV.MobileApp.Models.Vacunacion;

namespace SNCEV.MobileApp.Services;

public sealed class EsquemaVacunacionService
{
    private readonly HttpClient _http;

    public EsquemaVacunacionService(HttpClient http)
    {
        _http = http;
    }

    public async Task<EsquemaVacunacionResponse> ObtenerAsync(
        Guid pacienteId,
        CancellationToken cancellationToken = default)
    {
        var token = await SecureStorage.Default.GetAsync(
            "sncev_access_token");

        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException(
                "No existe una sesión activa.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"pacientes/{pacienteId:D}/esquema-vacunacion");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(
            request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException(
                "La sesión expiró.");

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException(
                "No tienes permisos para consultar el esquema.");

        response.EnsureSuccessStatusCode();

        var opciones = new JsonSerializerOptions(
            JsonSerializerDefaults.Web);

        var resultado = await response.Content
            .ReadFromJsonAsync<EsquemaVacunacionResponse>(
                opciones, cancellationToken);

        return resultado ?? throw new JsonException(
            "La API devolvió un esquema vacío.");
    }
}
