using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Infrastructure.Messaging;
using Kart.Search.Infrastructure.Rebuild;
using Kart.Search.Infrastructure.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kart.Search.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.Configure<OpenSearchOptions>(configuration.GetSection("OpenSearch"));
        services.Configure<ProductCatalogSnapshotOptions>(configuration.GetSection("ProductCatalogSnapshot"));

        // Message-bus-manifest-driven RabbitMQ topology (kart-identity-service's proven pattern) -
        // loaded once as a singleton; contracts/message-bus-manifest.json is copied to this
        // service's own output directory (Api.csproj) so this resolves without a fragile
        // "../../contracts" relative path once deployed as its own container image.
        services.AddSingleton(sp =>
        {
            var rabbitMqOptions = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            var manifestPath = Path.IsPathRooted(rabbitMqOptions.ManifestPath)
                ? rabbitMqOptions.ManifestPath
                : Path.Combine(AppContext.BaseDirectory, rabbitMqOptions.ManifestPath);
            return MessageBusManifestLoader.Load(manifestPath);
        });

        services.AddHostedService<RabbitMqTopologyStartupHostedService>();
        services.AddHostedService<ProductEventsConsumerHostedService>();
        services.AddHostedService<CategoryEventsConsumerHostedService>();
        services.AddHostedService<ReviewEventsConsumerHostedService>();

        // OpenSearch - this service's one and only data store (database-design.md). A typed
        // HttpClient (AddHttpClient) gives pooled, reused connections rather than a fresh
        // connection per call - important at this service's high-throughput target.
        services.AddHttpClient<OpenSearchHttpClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<OpenSearchOptions>>().Value;
            client.BaseAddress = new Uri(opts.Uri);
        });

        services.AddHostedService<OpenSearchIndexBootstrapHostedService>();

        services.AddSingleton<ISearchQueryRepository, OpenSearchSearchRepository>();
        services.AddSingleton<ISearchProjectionRepository, OpenSearchProjectionRepository>();
        services.AddSingleton<ICategoryLookupRepository, OpenSearchCategoryLookupRepository>();
        services.AddSingleton<IRatingLedgerRepository, OpenSearchRatingLedgerRepository>();
        services.AddSingleton<ISearchIndexAdmin, OpenSearchIndexAdmin>();

        // SRCH-9 (blue-green rebuild).
        services.AddSingleton<IRebuildCoordinator, RebuildCoordinator>();
        services.AddScoped<ICatalogSnapshotReader, NpgsqlProductCatalogSnapshotReader>();

        return services;
    }
}
