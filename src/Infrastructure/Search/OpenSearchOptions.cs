namespace Kart.Search.Infrastructure.Search;

/// <summary>Binds the <c>"OpenSearch"</c> config section. Index/alias names and shard counts are
/// this service's own fixed schema (database-design.md) - not meant to vary per environment, but
/// kept configurable so tests/dev can point at a lighter single-node topology.</summary>
public sealed class OpenSearchOptions
{
    public string Uri { get; set; } = "http://localhost:9200";

    public string ProductsAlias { get; set; } = "search-products-active";

    public string ProductsIndexPrefix { get; set; } = "search-products-";

    public string CategoryLookupIndex { get; set; } = "search-category-lookup";

    public string RatingLedgerIndex { get; set; } = "search-rating-ledger";

    /// <summary>8 shards routed by category.categoryId at full production scale
    /// (database-design.md, sized against a 100M-SKU catalog). Configurable down for local/dev/test
    /// single-node topologies, where a high shard count on zero replicas serves no purpose.</summary>
    public int ProductsShards { get; set; } = 8;

    public int ProductsReplicas { get; set; } = 1;

    public int RatingLedgerShards { get; set; } = 4;

    public int RatingLedgerReplicas { get; set; } = 1;

    public int CategoryLookupReplicas { get; set; } = 1;
}
