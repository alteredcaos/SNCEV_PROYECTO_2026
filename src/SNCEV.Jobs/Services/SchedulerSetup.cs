// =============================================================================
// SNCEV Jobs — Quartz.NET Job Definitions
// =============================================================================

using Quartz;
using Microsoft.Extensions.Logging;

namespace SNCEV.Jobs.Services;

public class CoberturaJob : IJob
{
    private readonly CoberturaWorkerService _worker;
    private readonly ILogger<CoberturaJob> _logger;

    public CoberturaJob(CoberturaWorkerService worker, ILogger<CoberturaJob> logger)
    {
        _worker = worker;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("▶ Executing Quartz Job: CoberturaJob");
        await _worker.EjecutarCalculoCoberturasAsync();
    }
}

public class LimpiezaTokensJob : IJob
{
    private readonly CoberturaWorkerService _worker;
    private readonly ILogger<LimpiezaTokensJob> _logger;

    public LimpiezaTokensJob(CoberturaWorkerService worker, ILogger<LimpiezaTokensJob> logger)
    {
        _worker = worker;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("▶ Executing Quartz Job: LimpiezaTokensJob");
        await _worker.LimpiarTokensExpiradosAsync();
    }
}
