// =============================================================================
// SNCEV Jobs — Servicio de Alertas (Slack Webhook & Email)
// =============================================================================

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SNCEV.Jobs.Alerts;

public class SlackAlertService
{
    private readonly ILogger<SlackAlertService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _webhookUrl;

    public SlackAlertService(ILogger<SlackAlertService> logger, IConfiguration config, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _webhookUrl = config["Slack:WebhookUrl"];
    }

    public async Task EnviarAlertaCoberturaAsync(List<dynamic> alertas, (int Anio, int Semana) semana, int filas, double duracionSeg, List<string> errores)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
        {
            _logger.LogInformation("Slack Webhook URL no configurada, omitiendo notificación.");
            return;
        }

        bool esExitoso = errores.Count == 0;
        string titulo = esExitoso ? "✅ Coberturas Calculadas Exitosamente" : "⚠️ Alerta / Errores en Job de Coberturas";

        var payload = new
        {
            text = $"*{titulo}*\n" +
                   $"• SE: *{semana.Semana:D2}/{semana.Anio}*\n" +
                   $"• Filas procesadas: `{filas}`\n" +
                   $"• Duración: `{duracionSeg:F2} seg`\n" +
                   $"• Estados en alerta (<95%): `{alertas.Count}`"
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_webhookUrl, content);
            _logger.LogInformation("Notificación Slack enviada con status code {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar alerta a Slack");
        }
    }
}
