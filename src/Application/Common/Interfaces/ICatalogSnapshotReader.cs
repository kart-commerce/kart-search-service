using Kart.Search.Application.Common.Models;

namespace Kart.Search.Application.Common.Interfaces;

/// <summary>
/// Rebuild-only (SRCH-9): a scheduled, paginated bulk read against a read-replica of
/// <c>kart-product-service</c>'s PostgreSQL write side (<c>variants</c>/<c>product_groups</c>) -
/// never its MongoDB read model (the Domain Invariant this mechanism exists to preserve,
/// architecture.md's "Index Rebuild Backfill Source"). Invoked only during an operator-triggered
/// reindex, off the live <c>/v1/search</c> request path.
/// </summary>
public interface ICatalogSnapshotReader
{
    IAsyncEnumerable<ProductSnapshotRow> ReadAllAsync(CancellationToken cancellationToken);
}
