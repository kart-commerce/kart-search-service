using Kart.Search.Application.Common.Models;
using Kart.Search.Domain.SearchDocuments;

namespace Kart.Search.Application.Common.Interfaces;

/// <summary>
/// The write side of the <c>search_products</c> projection (SRCH-1..4, SRCH-6) - every mutation
/// this service ever performs against its own index, all internal event-consumer writes, never a
/// caller-invoked write (ddd-model.md's CanRead/CanWrite/CanDelete invariant). Guarded catalog-origin
/// updates ("apply only if the incoming <c>occurredAt</c> is strictly newer than
/// <c>lastCatalogEventAt</c>") are enforced atomically inside the OpenSearch scripted update itself
/// (database-design.md's literal <c>POST search-products-active/_update/{sku}</c> spec) - not by
/// reading the document into <see cref="SearchDocument"/> and writing it back, which would race
/// concurrent consumers of different event types. The bool return value reports whether the guard
/// accepted the write (<c>true</c>) or rejected it as a stale-ordered redelivery (<c>false</c>),
/// purely for logging - a rejected write is not an error.
/// </summary>
public interface ISearchProjectionRepository
{
    /// <summary>Unconditional index (SRCH-1) - creation needs no guard, nothing precedes it.</summary>
    Task CreateAsync(SearchDocument document, CancellationToken cancellationToken);

    /// <summary>Guarded price update (SRCH-2).</summary>
    Task<bool> ApplyPriceChangeAsync(string sku, Money newPrice, DateTimeOffset occurredAt, CancellationToken cancellationToken);

    /// <summary>Guarded catalog-field update (SRCH-3) - only the non-null members of
    /// <paramref name="fields"/> are written.</summary>
    Task<bool> ApplyCatalogUpdateAsync(string sku, CatalogUpdateFields fields, DateTimeOffset occurredAt, CancellationToken cancellationToken);

    /// <summary>Soft-remove (SRCH-4) - same guarded write path, no second removal code path.</summary>
    Task<bool> MarkDiscontinuedAsync(string sku, DateTimeOffset discontinuedAt, CancellationToken cancellationToken);

    /// <summary>Rating-origin update (SRCH-6) - deliberately unguarded (disjoint field set from the
    /// catalog-origin guard, ddd-model.md); the recomputed <see cref="RatingSignal"/> is already
    /// idempotent by construction. A no-op if no document exists yet for the SKU (a review can
    /// arrive before the corresponding ProductCreated in a genuinely pathological ordering; this
    /// mirrors requirement-spec's "never blocks" posture rather than erroring).</summary>
    Task ApplyRatingAsync(string sku, RatingSignal rating, CancellationToken cancellationToken);
}
