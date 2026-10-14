// =============================================================================
// SNCEV Jobs — CoberturaWorkerService
// Ejecución de Stored Procedure sp_CalcularCobertura en SQL Server y Alertas
// =============================================================================

using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SNCEV.Jobs.Alerts;
using SNCEV.Jobs.Monitoring;

namespace SNCEV.Jobs.Services;

public class CoberturaWorkerService
{
    private readonly ILogger<CoberturaWorkerService> _logger;
    private readonly IConfiguration _config;
    private readonly SlackAlertService _slackAlerts;

    public CoberturaWorkerService(
        ILogger<CoberturaWorkerService> logger,
        IConfiguration config,
        SlackAlertService slackAlerts)
    {
        _logger = logger;
        _config = config;
        _slackAlerts = slackAlerts;
    }

    public async Task EjecutarCalculoCoberturasAsync(List<(int Anio, int Semana)>? semanas = null)
    {
        semanas ??= new List<(int, int)> { SemanaEpidemiologicaService.SemanaAnterior() };
        var connString = _config.GetConnectionString("SqlServerConnection");
        var stopwatch = Stopwatch.StartNew();
        int totalFilas = 0;
        List<string> errores = new();
        List<dynamic> estadosAlerta = new();

        _logger.LogInformation("Iniciando Job de Coberturas en C# .NET 8...");

        using var conn = new SqlConnection(connString);
        await conn.OpenAsync();

        foreach (var (anio, semana) in semanas)
        {
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@Anio", anio, DbType.Int16);
                parameters.Add("@Semana", semana, DbType.Int16);
                parameters.Add("@FilasAfectadas", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await conn.ExecuteAsync(
                    "sncev_report.sp_CalcularCobertura",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                int filas = parameters.Get<int>("@FilasAfectadas");
                totalFilas += filas;
                MetricsExporter.FilasCalculadas.WithLabels(anio.ToString(), semana.ToString()).Inc(filas);

                _logger.LogInformation("SE {Semana}/{Anio} calculada exitosamente. Filas: {Filas}", semana, anio, filas);
            }
            catch (Exception ex)
            {
                string msg = $"Error calculando SE {semana}/{anio}: {ex.Message}";
                _logger.LogError(ex, "{Msg}", msg);
                errores.Add(msg);
                MetricsExporter.ErroresJob.WithLabels("semana").Inc();
            }
        }

        // Detectar alertas de cobertura bajo el 95%
        try
        {
            var ultimaSemana = semanas.Last();
            decimal umbral = decimal.Parse(_config["UmbralAlertaPorcentaje"] ?? "95.00");

            var queryAlertas = @"
                SELECT
                    e.nombre AS Estado,
                    v.nombre_generico AS Vacuna,
                    c.numero_dosis AS Dosis,
                    ROUND(SUM(CAST(c.dosis_aplicadas AS FLOAT)) / NULLIF(SUM(c.poblacion_objetivo), 0) * 100, 2) AS CoberturaPct
                FROM sncev_report.cobertura_vacunacion c
                INNER JOIN sncev_geo.estado e ON c.estado_clave = e.clave_inegi
                INNER JOIN sncev.vacuna_catalogo v ON c.vacuna_id = v.id
                WHERE c.anio = @Anio AND c.semana_epidemiologica = @Semana
                GROUP BY e.nombre, v.nombre_generico, c.numero_dosis
                HAVING ROUND(SUM(CAST(c.dosis_aplicadas AS FLOAT)) / NULLIF(SUM(c.poblacion_objetivo), 0) * 100, 2) < @Umbral";

            estadosAlerta = (await conn.QueryAsync(queryAlertas, new { Anio = ultimaSemana.Anio, Semana = ultimaSemana.Semana, Umbral = umbral })).ToList();
            MetricsExporter.EstadosEnAlerta.Set(estadosAlerta.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al detectar alertas de cobertura");
        }

        stopwatch.Stop();
        double duracionSeg = stopwatch.Elapsed.TotalSeconds;
        MetricsExporter.DuracionJob.Observe(duracionSeg);

        // Registrar en bitácora audit_log
        try
        {
            var datosNuevosJson = JsonSerializer.Serialize(new
            {
                job = "CalcularCobertura",
                filas = totalFilas,
                duracionSeg = Math.Round(duracionSeg, 2),
                alertasCount = estadosAlerta.Count,
                errores
            });

            await conn.ExecuteAsync(@"
                INSERT INTO sncev_audit.audit_log (accion, modulo, tabla_afectada, datos_nuevos, resultado)
                VALUES ('INSERT', 'Jobs', 'sncev_report.cobertura_vacunacion', @DatosNuevos, @Resultado)",
                new { DatosNuevos = datosNuevosJson, Resultado = errores.Count == 0 ? "OK" : "ERROR" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando auditoría en DB");
        }

        // Notificar a Slack
        await _slackAlerts.EnviarAlertaCoberturaAsync(estadosAlerta, semanas.Last(), totalFilas, duracionSeg, errores);
    }

    public async Task LimpiarTokensExpiradosAsync()
    {
        var connString = _config.GetConnectionString("SqlServerConnection");
        using var conn = new SqlConnection(connString);
        int eliminados = await conn.ExecuteAsync("DELETE FROM sncev_admin.sesion_token WHERE fecha_expiracion < SYSDATETIME() OR revocado = 1");
        _logger.LogInformation("Tokens expirados o revocados eliminados: {Cantidad}", eliminados);
    }
}
