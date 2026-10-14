// =============================================================================
// SNCEV Jobs — Exportador de Métricas Prometheus (prometheus-net)
// =============================================================================

using Prometheus;

namespace SNCEV.Jobs.Monitoring;

public static class MetricsExporter
{
    public static readonly Counter FilasCalculadas = Metrics.CreateCounter(
        "sncev_cobertura_filas_total",
        "Filas en cobertura_vacunacion",
        new CounterConfiguration { LabelNames = new[] { "anio", "semana" } }
    );

    public static readonly Histogram DuracionJob = Metrics.CreateHistogram(
        "sncev_cobertura_duracion_seg",
        "Duración del job",
        new HistogramConfiguration
        {
            Buckets = new[] { 5.0, 10.0, 30.0, 60.0, 120.0, 300.0, 600.0 }
        }
    );

    public static readonly Gauge EstadosEnAlerta = Metrics.CreateGauge(
        "sncev_cobertura_estados_alerta",
        "Estados por debajo del umbral"
    );

    public static readonly Counter ErroresJob = Metrics.CreateCounter(
        "sncev_cobertura_errores_total",
        "Errores del job",
        new CounterConfiguration { LabelNames = new[] { "tipo" } }
    );
}
