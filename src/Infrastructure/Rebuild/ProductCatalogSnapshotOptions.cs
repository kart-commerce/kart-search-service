namespace Kart.Search.Infrastructure.Rebuild;

/// <summary>Binds the <c>"ProductCatalogSnapshot"</c> config section - a read-only connection
/// string pointed at a read-replica of kart-product-service's PostgreSQL write side
/// (database-design.md's Rebuild Backfill Mechanism). Cross-repo, operator-provided in real
/// deployments; this service never owns or migrates that schema.</summary>
public sealed class ProductCatalogSnapshotOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Rows fetched per keyset-pagination round-trip.</summary>
    public int BatchSize { get; set; } = 500;
}
