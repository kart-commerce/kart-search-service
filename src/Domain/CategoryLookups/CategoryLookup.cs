namespace Kart.Search.Domain.CategoryLookups;

/// <summary>
/// Aggregate root, identified by <c>CategoryId</c> (referenced, owned by kart-category-service -
/// never redefined here, ddd-model.md). One entry per category node this service has ever seen
/// referenced by an indexed <c>SearchDocument</c>. Deliberately a separate, small aggregate, not a
/// field embedded per-document: many <c>SearchDocument</c>s share one <c>categoryId</c>, and a
/// category rename must never fan out into a write to every affected document (ddd-model.md's
/// Modeling Decision 1). A <c>SearchDocument</c>'s own <c>category.categoryName</c> is therefore a
/// denormalized, eventually-consistent snapshot taken at that document's own last catalog-origin
/// write - never live-joined at query time.
/// </summary>
public sealed class CategoryLookup
{
    public string CategoryId { get; private set; }
    public string CategoryName { get; private set; }
    public DateTimeOffset LastUpdatedAt { get; private set; }

    private CategoryLookup(string categoryId, string categoryName, DateTimeOffset lastUpdatedAt)
    {
        CategoryId = categoryId;
        CategoryName = categoryName;
        LastUpdatedAt = lastUpdatedAt;
    }

    public static CategoryLookup Create(string categoryId, string categoryName, DateTimeOffset occurredAt) =>
        new(categoryId, categoryName, occurredAt);

    /// <summary>Guarded the same way <c>SearchDocument</c>'s catalog-origin fields are - a
    /// <c>CategoryUpdated</c> older than the stored value is rejected as a stale redelivery.</summary>
    public bool CanApply(DateTimeOffset occurredAt) => occurredAt > LastUpdatedAt;

    public bool Apply(string categoryName, DateTimeOffset occurredAt)
    {
        if (!CanApply(occurredAt))
        {
            return false;
        }

        CategoryName = categoryName;
        LastUpdatedAt = occurredAt;
        return true;
    }
}
