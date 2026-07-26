using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kart.Search.Infrastructure.Search;

/// <summary>
/// Idempotent create-if-absent bootstrap for this service's three indices + the
/// <c>search-products-active</c> alias, run once at startup - the equivalent of an EF Core
/// migration for a service with no relational write model. An OpenSearch outage at boot never
/// crashes the process (matching the same degrade-not-crash posture the RabbitMQ topology startup
/// service takes) - logged as a warning; every write/query path simply fails until OpenSearch
/// becomes reachable and this can be safely re-run.
/// </summary>
public sealed class OpenSearchIndexBootstrapHostedService(
    OpenSearchHttpClient client,
    IOptions<OpenSearchOptions> options,
    ILogger<OpenSearchIndexBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;

        try
        {
            if (!await client.ExistsAsync($"/{opts.ProductsAlias}", cancellationToken))
            {
                var firstIndexName = $"{opts.ProductsIndexPrefix}000001";
                await client.PutAsync($"/{firstIndexName}", OpenSearchIndexMappings.Products(opts.ProductsShards, opts.ProductsReplicas), cancellationToken);
                await client.PostAsync(
                    "/_aliases",
                    new { actions = new object[] { new { add = new { index = firstIndexName, alias = opts.ProductsAlias } } } },
                    cancellationToken);
                logger.LogInformation("Bootstrapped {Index}, aliased as {Alias}", firstIndexName, opts.ProductsAlias);
            }

            if (!await client.ExistsAsync($"/{opts.CategoryLookupIndex}", cancellationToken))
            {
                await client.PutAsync($"/{opts.CategoryLookupIndex}", OpenSearchIndexMappings.CategoryLookup(opts.CategoryLookupReplicas), cancellationToken);
                logger.LogInformation("Bootstrapped {Index}", opts.CategoryLookupIndex);
            }

            if (!await client.ExistsAsync($"/{opts.RatingLedgerIndex}", cancellationToken))
            {
                await client.PutAsync($"/{opts.RatingLedgerIndex}", OpenSearchIndexMappings.RatingLedger(opts.RatingLedgerShards, opts.RatingLedgerReplicas), cancellationToken);
                logger.LogInformation("Bootstrapped {Index}", opts.RatingLedgerIndex);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not bootstrap OpenSearch indices at startup - the process is not crashing, but reads/writes will fail until OpenSearch is reachable");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
