namespace Kart.Search.Infrastructure.Search;

/// <summary>The three index mappings/settings this service owns, exactly as specified by
/// database-design.md - shared between the bootstrap hosted service (first index) and the
/// rebuild pipeline (every subsequent <c>search-products-NNNNNN</c> generation).</summary>
public static class OpenSearchIndexMappings
{
    public static object Products(int shards, int replicas) => new
    {
        settings = new
        {
            number_of_shards = shards,
            number_of_replicas = replicas,
            refresh_interval = "1s",
            index = new { max_result_window = 10_000 },
        },
        mappings = new
        {
            properties = new Dictionary<string, object>
            {
                ["sku"] = new { type = "keyword" },
                ["name"] = new { type = "text" },
                ["description"] = new { type = "text" },
                ["brand"] = new { type = "text", fields = new { keyword = new { type = "keyword" } } },
                // Never searched/aggregated - display-only, so disable indexing entirely (same
                // rationale as extendedAttributesRaw below).
                ["imageUrl"] = new { type = "keyword", index = false },
                ["category"] = new
                {
                    properties = new
                    {
                        categoryId = new { type = "keyword" },
                        categoryName = new { type = "keyword" },
                    },
                },
                ["price"] = new
                {
                    properties = new
                    {
                        amount = new { type = "scaled_float", scaling_factor = 100 },
                        currency = new { type = "keyword" },
                    },
                },
                ["availability"] = new { type = "keyword" },
                ["size"] = new { type = "keyword" },
                ["color"] = new { type = "keyword" },
                ["sponsored"] = new { type = "boolean" },
                // Schemaless catch-all (database-design.md: "never facet-queried, present only so
                // a future requirement can add a new facet without a mapping migration"). Mapped
                // as an unindexed object rather than "flattened" - functionally equivalent for
                // this field's own purpose (stored in _source, never searched/aggregated), and
                // avoids per-key dynamic-type inference entirely (arbitrary shapes across
                // different events' attribute bags never risk a mapping conflict).
                ["extendedAttributesRaw"] = new { type = "object", enabled = false },
                ["rating"] = new
                {
                    properties = new
                    {
                        avg = new { type = "double" },
                        count = new { type = "integer" },
                    },
                },
                ["lastCatalogEventAt"] = new { type = "date" },
                ["lastUpdatedAt"] = new { type = "date" },
            },
        },
    };

    public static object CategoryLookup(int replicas) => new
    {
        settings = new { number_of_shards = 1, number_of_replicas = replicas },
        mappings = new
        {
            properties = new
            {
                categoryId = new { type = "keyword" },
                categoryName = new { type = "keyword" },
                lastUpdatedAt = new { type = "date" },
            },
        },
    };

    public static object RatingLedger(int shards, int replicas) => new
    {
        settings = new { number_of_shards = shards, number_of_replicas = replicas },
        mappings = new
        {
            properties = new
            {
                sku = new { type = "keyword" },
                // Write-side-only bookkeeping, never queried directly - unindexed object avoids
                // any dynamic-type conflict across differently-shaped per-review maps (see the
                // identical reasoning on search_products.extendedAttributesRaw above).
                perReviewRatings = new { type = "object", enabled = false },
            },
        },
    };
}
