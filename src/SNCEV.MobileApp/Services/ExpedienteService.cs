
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SNCEV.MobileApp.Models.Expediente;

namespace SNCEV.MobileApp.Services;

public sealed class ExpedienteService
{
    private readonly HttpClient _http;

    public ExpedienteService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ExpedientePacienteResponse> ObtenerAsync(
        Guid pacienteId,
        CancellationToken cancellationToken = default)
    {
        var token = await SecureStorage.Default.GetAsync(
            "sncev_access_token");

        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException(
                "La sesión no está disponible.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"pacientes/{pacienteId:D}/expediente");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(
            request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException(
                "La sesión expiró.");

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException(
                "No tienes permisos para consultar el expediente.");

        response.EnsureSuccessStatusCode();

        var opciones = new JsonSerializerOptions(
            JsonSerializerDefaults.Web);

        return await response.Content.ReadFromJsonAsync<
            ExpedientePacienteResponse>(
                opciones, cancellationToken)
            ?? throw new JsonException(
                "La API devolvió un expediente vacío.");
    }
}
