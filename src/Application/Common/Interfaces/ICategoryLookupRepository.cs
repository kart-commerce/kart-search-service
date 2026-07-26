namespace Kart.Search.Application.Common.Interfaces;

/// <summary>Backs the <c>CategoryLookup</c> aggregate / <c>search_category_lookup</c> index
/// (SRCH-5). Consulted (never joined at query time) by the <c>SearchDocument</c> write path to
/// resolve <c>category.categoryName</c> - ddd-model.md's Cross-Aggregate Interaction.</summary>
public interface ICategoryLookupRepository
{
    /// <summary><c>null</c> if no <c>CategoryUpdated</c> has been received yet for this id - never
    /// blocks the caller, per requirement-spec's Domain Invariant.</summary>
    Task<string?> GetCategoryNameAsync(string categoryId, CancellationToken cancellationToken);

    /// <summary>Guarded upsert - <c>true</c> if applied, <c>false</c> if rejected as a stale
    /// redelivery (older than the stored <c>lastUpdatedAt</c>).</summary>
    Task<bool> UpsertAsync(string categoryId, string categoryName, DateTimeOffset occurredAt, CancellationToken cancellationToken);
}
