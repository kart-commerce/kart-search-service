using Kart.Search.Application.Common.Models;

namespace Kart.Search.Application.Common.Interfaces;

/// <summary>Index-lifecycle operations for the blue-green rebuild (SRCH-9, design-decisions.md's
/// "Index Rebuild Strategy") - creating a new versioned index, bulk-loading a Postgres snapshot
/// into it, and atomically swapping the <c>search-products-active</c> alias once caught up. The
/// old index is left in place afterward for manual operator cleanup (safer default than
/// auto-delete).</summary>
public interface ISearchIndexAdmin
{
    /// <summary>Creates a new, empty <c>search-products-NNNNNN</c> index with the standard
    /// mapping/settings, and returns its name.</summary>
    Task<string> CreateNewProductsIndexAsync(CancellationToken cancellationToken);

    /// <summary>Bulk-indexes a full snapshot row directly into the named index (bypasses the
    /// guarded update path - a rebuild backfill is unconditional, matching how ProductCreated
    /// itself needs no guard).</summary>
    Task BulkIndexAsync(string indexName, ProductSnapshotRow row, CancellationToken cancellationToken);

    /// <summary>Atomically repoints the <c>search-products-active</c> alias to <paramref name="indexName"/>.</summary>
    Task SwapAliasAsync(string indexName, CancellationToken cancellationToken);
}
