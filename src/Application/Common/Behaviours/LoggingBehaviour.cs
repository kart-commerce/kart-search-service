using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Application.Common.Behaviours;

/// <summary>Generic completion log for every MediatR request - request name + elapsed ms,
/// deliberately never the request/response payload itself (no secrets to leak here, but this
/// matches the platform's standard behaviour verbatim). This is not the place exceptions are
/// logged - the global exception handler (Kart.Shared.ErrorHandling) is the single place any
/// exception reaching the HTTP boundary is logged (kart-conventions.md).</summary>
public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        // Checkpoint-logging taxonomy stage 3 ("<Command>HandlerStarted", first line inside
        // Handle()) generalized here rather than duplicated in every handler - this behavior
        // already wraps every MediatR request (SearchProductsQuery and every Consume* command
        // alike), so it's the one place that's true by construction.
        logger.LogInformation("Stage {Stage}: {RequestName} handler started", $"{requestName}HandlerStarted", requestName);

        var response = await next();
        stopwatch.Stop();

        logger.LogInformation("Stage {Stage}: {RequestName} completed in {ElapsedMs}ms", $"{requestName}Completed", requestName, stopwatch.ElapsedMilliseconds);

        return response;
    }
}
