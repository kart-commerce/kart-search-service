namespace Kart.Search.Domain.SearchDocuments;

/// <summary>
/// Aggregate root, identified by <c>Sku</c> (referenced, not redefined - owned by
/// kart-product-service, ddd-model.md's ACL rule). One document per SKU, the entire unit
/// <c>GET /v1/search</c> queries and ranks over.
///
/// This is a rebuildable read-side projection (database-design.md's Architecture Note), not an
/// EF-persisted write-side aggregate - the guard methods below express this service's own
/// invariants in a single, unit-testable place (ddd-model.md), but the actual concurrency-safe
/// enforcement against concurrent redeliveries happens server-side, via an equivalent OpenSearch
/// Painless script guard on the real write path (Infrastructure/Search/OpenSearchProjectionRepository) -
/// the two must stay in lockstep. A <see cref="SearchDocument"/> may only come into existence by
/// consuming <c>ProductCreated</c> (<see cref="Create"/>) - there is no "pending" placeholder state.
/// </summary>
public sealed class SearchDocument
{
    public string Sku { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public CategoryRef Category { get; private set; }
    public string? Brand { get; private set; }
    public string? ImageUrl { get; private set; }
    public Money Price { get; private set; }
    public Availability Availability { get; private set; }
    public FacetableAttributes Attributes { get; private set; }
    public RatingSignal Rating { get; private set; }

    /// <summary>The <c>occurredAt</c>/creation-time of the last accepted ProductCreated/
    /// ProductPriceChanged/ProductUpdated/ProductDiscontinued - the version/timestamp guard field
    /// (ddd-model.md's Invariants). Never written by a rating-origin update (disjoint field set).</summary>
    public DateTimeOffset LastCatalogEventAt { get; private set; }

    /// <summary>This service's own last-write timestamp, any source (catalog or rating).</summary>
    public DateTimeOffset LastUpdatedAt { get; private set; }

    private SearchDocument(
        string sku,
        string name,
        string? description,
        CategoryRef category,
        string? brand,
        string? imageUrl,
        Money price,
        Availability availability,
        FacetableAttributes attributes,
        RatingSignal rating,
        DateTimeOffset lastCatalogEventAt,
        DateTimeOffset lastUpdatedAt)
    {
        Sku = sku;
        Name = name;
        Description = description;
        Category = category;
        Brand = brand;
        ImageUrl = imageUrl;
        Price = price;
        Availability = availability;
        Attributes = attributes;
        Rating = rating;
        LastCatalogEventAt = lastCatalogEventAt;
        LastUpdatedAt = lastUpdatedAt;
    }

    /// <summary>The aggregate-creation trigger (ddd-model.md) - consuming <c>ProductCreated</c>.
    /// Unconditional: creation needs no version guard, nothing precedes it. <paramref name="categoryName"/>
    /// is resolved from <see cref="Kart.Search.Domain.CategoryLookups.CategoryLookup"/> at write
    /// time by the caller, or left <c>null</c> if no lookup entry exists yet.</summary>
    public static SearchDocument Create(
        string sku,
        string name,
        string? description,
        string categoryId,
        string? categoryName,
        string? brand,
        Money price,
        FacetableAttributes attributes,
        DateTimeOffset occurredAt,
        DateTimeOffset now,
        string? imageUrl = null) =>
        new(
            sku,
            name,
            description,
            new CategoryRef(categoryId, categoryName),
            brand,
            imageUrl,
            price,
            Availability.Active,
            attributes,
            RatingSignal.None,
            occurredAt,
            now);

    /// <summary>Whether an incoming catalog-origin event may be applied - strictly newer than the
    /// stored <see cref="LastCatalogEventAt"/>, otherwise rejected as a stale-ordered redelivery
    /// (edge-cases.md's "Out-of-Order Event Consumption").</summary>
    public bool CanApplyCatalogEvent(DateTimeOffset occurredAt) => occurredAt > LastCatalogEventAt;

    /// <summary>Guarded price update (SRCH-2). Returns <c>false</c> (no-op) if the guard rejects it.</summary>
    public bool ApplyPriceChange(Money newPrice, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        if (!CanApplyCatalogEvent(occurredAt))
        {
            return false;
        }

        Price = newPrice;
        LastCatalogEventAt = occurredAt;
        LastUpdatedAt = now;
        return true;
    }

    /// <summary>Guarded catalog-field update (SRCH-3) - only the fields Product's own
    /// <c>changedFields</c> named are touched; a <c>null</c> parameter means "not changed",
    /// distinct from an explicit clear (kart-product-service's own convention for this payload
    /// shape). <paramref name="categoryName"/> is re-resolved by the caller only when
    /// <c>changedFields</c> includes the category (ddd-model.md).</summary>
    public bool ApplyCatalogUpdate(
        string? name,
        string? description,
        string? categoryId,
        string? categoryName,
        string? brand,
        FacetableAttributes? attributes,
        DateTimeOffset occurredAt,
        DateTimeOffset now,
        string? imageUrl = null)
    {
        if (!CanApplyCatalogEvent(occurredAt))
        {
            return false;
        }

        if (name is not null)
        {
            Name = name;
        }

        if (description is not null)
        {
            Description = description;
        }

        if (categoryId is not null)
        {
            Category = new CategoryRef(categoryId, categoryName);
        }

        if (brand is not null)
        {
            Brand = brand;
        }

        if (imageUrl is not null)
        {
            ImageUrl = imageUrl;
        }

        if (attributes is not null)
        {
            Attributes = attributes;
        }

        LastCatalogEventAt = occurredAt;
        LastUpdatedAt = now;
        return true;
    }

    /// <summary>Soft-remove (SRCH-4, edge-cases.md "Discontinued Product Still Returned by
    /// Search") - excludes the document from default result sets, never hard-deletes, and reuses
    /// this same guarded write path rather than a second removal code path.</summary>
    public bool MarkDiscontinued(DateTimeOffset discontinuedAt, DateTimeOffset now)
    {
        if (!CanApplyCatalogEvent(discontinuedAt))
        {
            return false;
        }

        Availability = Availability.Discontinued;
        LastCatalogEventAt = discontinuedAt;
        LastUpdatedAt = now;
        return true;
    }

    /// <summary>Rating-origin update (SRCH-6) - deliberately NOT guarded by
    /// <see cref="LastCatalogEventAt"/> (disjoint field set, ddd-model.md/database-design.md): the
    /// recomputed <see cref="RatingSignal"/> is already idempotent by construction, and this field
    /// is never touched by a catalog-origin write, so no shared version field is needed for this
    /// pair specifically.</summary>
    public void ApplyRating(RatingSignal rating, DateTimeOffset now)
    {
        Rating = rating;
        LastUpdatedAt = now;
    }
}
