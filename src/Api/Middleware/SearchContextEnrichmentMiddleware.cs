using Serilog.Context;

namespace Kart.Search.Api.Middleware;

/// <summary>
/// Pushes a request-scoped <c>queryId</c> into Serilog's <see cref="LogContext"/> for every
/// request, so it appears on every subsequent log line for that request - requirement-spec.md's
/// stated correlation field for <c>GET /v1/search</c> spans/logs, since Search's public read has
/// no per-user or per-entity ownership dimension to key on instead.
/// </summary>
public sealed class SearchContextEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var queryId = Guid.NewGuid();
        context.Response.Headers["X-Query-Id"] = queryId.ToString();

        using (LogContext.PushProperty("queryId", queryId))
        {
            await next(context);
        }
    }
}
