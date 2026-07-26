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
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        logger.LogInformation("{RequestName} completed in {ElapsedMs}ms", typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);

        return response;
    }
}
