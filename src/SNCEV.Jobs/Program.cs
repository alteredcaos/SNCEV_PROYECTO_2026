using Quartz;
using Prometheus;
using SNCEV.Jobs.Alerts;
using SNCEV.Jobs.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configurar servicios
builder.Services.AddHttpClient<SlackAlertService>();
builder.Services.AddSingleton<CoberturaWorkerService>();

// Configurar Quartz.NET Scheduler
builder.Services.AddQuartz(q =>
{
    // Job Cobertura semanal: Lunes 03:00 UTC
    var coberturaKey = new JobKey("CoberturaJob");
    q.AddJob<CoberturaJob>(opts => opts.WithIdentity(coberturaKey));
    q.AddTrigger(opts => opts
        .ForJob(coberturaKey)
        .WithIdentity("CoberturaTrigger")
        .WithCronSchedule("0 0 3 ? * MON", x => x.InTimeZone(TimeZoneInfo.Utc)));

    // Job Limpieza diario: 04:00 UTC
    var limpiezaKey = new JobKey("LimpiezaTokensJob");
    q.AddJob<LimpiezaTokensJob>(opts => opts.WithIdentity(limpiezaKey));
    q.AddTrigger(opts => opts
        .ForJob(limpiezaKey)
        .WithIdentity("LimpiezaTrigger")
        .WithCronSchedule("0 0 4 * * ?", x => x.InTimeZone(TimeZoneInfo.Utc)));
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();

// Iniciar Servidor de Métricas Prometheus en el puerto 9090
var metricServer = new MetricServer(port: 9090);
metricServer.Start();

host.Run();
