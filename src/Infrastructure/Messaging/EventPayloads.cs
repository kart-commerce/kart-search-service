namespace Kart.Search.Infrastructure.Messaging;

/// <summary>
/// event-contract.md's consumed-event payload shapes - owned by their respective publishers
/// (kart-product-service/kart-category-service/kart-review-service), not this service; kept local
/// to Infrastructure rather than in the manifest, since a manifest only ever describes topology,
/// never payload schemas. Money/attributes shapes mirror kart-product-service's own
/// <c>Money</c>/<c>ProductAttributes</c> value objects exactly (ADR-0018's enriched payloads).
/// </summary>
public sealed record MoneyPayload(decimal Amount, string Currency);

public sealed record AttributesPayload(string? Size, string? Color, IReadOnlyDictionary<string, object?>? ExtendedAttributes);

/// <summary>Enriched by ADR-0018 to the full initial snapshot needed to build a complete
/// searchable document at creation time (originally just <c>sku, attributes</c>).</summary>
public sealed record ProductCreatedPayload(
    string Sku,
    string Name,
    string? Description,
    string CategoryId,
    string? Brand,
    MoneyPayload Price,
    string Status,
    AttributesPayload Attributes,
    DateTimeOffset OccurredAt,
    string? ImageUrl = null);

public sealed record ProductPriceChangedPayload(string Sku, MoneyPayload OldPrice, MoneyPayload NewPrice, DateTimeOffset OccurredAt);

/// <summary>Enriched by ADR-0018 (originally just <c>sku, changedFields, occurredAt</c>) - always
/// the current full value for any field named in <see cref="ChangedFields"/>.</summary>
public sealed record ProductUpdatedPayload(
    string Sku,
    IReadOnlyList<string> ChangedFields,
    DateTimeOffset OccurredAt,
    string? Name,
    string? Description,
    string? CategoryId,
    string? Brand,
    string? Status,
    AttributesPayload? Attributes,
    string? ImageUrl = null);

public sealed record ProductDiscontinuedPayload(string Sku, DateTimeOffset DiscontinuedAt);

/// <summary><c>parentId</c>/<c>path</c> are received but not consumed further by this service -
/// Search only needs the leaf <c>categoryId -&gt; name</c> mapping (event-contract.md).
/// <c>Path</c> must stay an array of ids to match what kart-category-service actually publishes
/// (<c>CategoryUpdatedEventPayload.Path</c>, an <c>IReadOnlyList&lt;Guid&gt;</c> materialized path) -
/// this was previously typed <c>string?</c> here, which failed deserialization of the *entire*
/// payload for every single CategoryUpdated event ever published (found 2026-08-11 via 73 messages
/// stuck in search.category-events.dlq).</summary>
public sealed record CategoryUpdatedPayload(string CategoryId, string Name, string? ParentId, IReadOnlyList<Guid>? Path, string Operation, DateTimeOffset OccurredAt);

public sealed record ReviewSubmittedPayload(string OrderId, string Sku, double Rating, string ReviewId, string UserId);

public sealed record ReviewUpdatedPayload(string OrderId, string Sku, double OldRating, double NewRating);
