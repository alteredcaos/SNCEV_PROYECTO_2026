using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SNCEV.Api.Middleware;

/// <summary>Respuesta genérica para excepciones no controladas; detalles solo en logs internos.</summary>
public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cliente desconectado: no escribir respuesta.
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            _logger.LogError(ex, "Error no controlado. TraceId={TraceId} Metodo={Metodo} Ruta={Ruta}",
                traceId, context.Request.Method, context.Request.Path.Value);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno del servidor",
                Detail = "La operación no pudo completarse. Contacte al administrador con el identificador de seguimiento.",
                Instance = context.Request.Path.Value
            };
            problem.Extensions["traceId"] = traceId;
            await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
        }
    }
}
