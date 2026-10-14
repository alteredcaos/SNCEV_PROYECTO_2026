using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SNCEV.MobileApp.Services;

public sealed class DomicilioVigenteDto
{
    public string? EstadoNombre { get; set; }
    public string? MunicipioNombre { get; set; }
    public string? Localidad { get; set; }
    public string? Colonia { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? DireccionTexto { get; set; }
    public string? AgebClave { get; set; }
    public string? Cvegeo { get; set; }
    public string? CluesSugerida { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public bool Vigente { get; set; }
}

public sealed class DomiciliosService
{
    private readonly HttpClient _http;

    public DomiciliosService(HttpClient http)
    {
        _http = http;
    }

    public async Task<DomicilioVigenteDto?> ObtenerVigenteAsync(
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
            $"pacientes/{pacienteId:D}/domicilios/vigente");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(
            request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException(
                "La sesión expiró.");

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException(
                "No tienes permisos para consultar el domicilio.");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DomicilioVigenteDto>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken)
            ?? throw new JsonException(
                "La API devolvió un domicilio vacío.");
    }
}