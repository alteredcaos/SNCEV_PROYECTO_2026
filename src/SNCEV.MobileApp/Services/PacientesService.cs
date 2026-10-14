
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SNCEV.MobileApp.Models;

namespace SNCEV.MobileApp.Services;

public sealed class PacientesService
{
    private readonly HttpClient _http;

    public PacientesService(HttpClient http)
    {
        _http = http;
    }


    public async Task<PaginaPacientes> BuscarAsync(
        string criterio,
        CancellationToken cancellationToken = default)
    {
        criterio = criterio.Trim();

        if (string.IsNullOrWhiteSpace(criterio))
            return new PaginaPacientes();

        var token = await SecureStorage.Default.GetAsync(
            "sncev_access_token");

        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException(
                "La sesión no está disponible.");

        // Detectar si el criterio es una CURP de 18 caracteres.
        bool esCurp = System.Text.RegularExpressions.Regex.IsMatch(
            criterio,
            @"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // CURP -> parámetro curp
        // Nombre -> parámetro nombre
        string parametro = esCurp ? "curp" : "nombre";

        // Construir la URL con un solo criterio.
        string url =
            $"pacientes?{parametro}={Uri.EscapeDataString(criterio)}" +
            "&page=1&pageSize=25";

        using var request = new HttpRequestMessage(
            HttpMethod.Get, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(
            request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException(
                "La sesión expiró. Inicia sesión nuevamente.");

        response.EnsureSuccessStatusCode();

        var pagina = await response.Content.ReadFromJsonAsync<PaginaPacientes>(
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        return pagina ?? new PaginaPacientes();
    }



    public async Task<PacienteResumen?> ObtenerPorIdAsync(
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
            $"pacientes/{pacienteId:D}");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", token);

        using var response = await _http.SendAsync(
            request, cancellationToken);

        if (response.StatusCode ==
            System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException(
                "La sesión expiró.");
        }

        if (response.StatusCode ==
            System.Net.HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                "No tienes permisos para consultar este expediente.");
        }

        if (response.StatusCode ==
            System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<
            PacienteResumen>(
                cancellationToken: cancellationToken);
    }

}

public sealed class PaginaPacientes
{
    public List<PacienteResumen> Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int Total { get; set; }

    public int TotalPages { get; set; }
}
