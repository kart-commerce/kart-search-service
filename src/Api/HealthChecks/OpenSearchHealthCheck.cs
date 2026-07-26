using Kart.Search.Infrastructure.Search;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kart.Search.Api.HealthChecks;

/// <summary>Readiness signal for the k8s Helm chart's <c>/health/ready</c> probe - this service's
/// entire job depends on OpenSearch being reachable (it is this service's one and only data
/// store), so readiness genuinely means "OpenSearch responds," not just "the process is up."</summary>
public sealed class OpenSearchHealthCheck(OpenSearchHttpClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await client.GetRawAsync("/_cluster/health", cancellationToken);
            return result is not null
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("OpenSearch cluster health endpoint returned no body");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("OpenSearch is unreachable", exception);
        }
    }
}
