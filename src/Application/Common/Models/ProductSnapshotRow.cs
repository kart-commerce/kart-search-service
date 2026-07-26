namespace Kart.Search.Application.Common.Models;

/// <summary>One row read back from <c>kart-product-service</c>'s PostgreSQL write side
/// (<c>product_groups</c>/<c>variants</c>) during a blue-green rebuild backfill
/// (database-design.md's Rebuild Backfill Mechanism) - shaped identically to a
/// <c>ProductCreatedPayload</c>, since both build the same initial <c>SearchDocument</c> snapshot.</summary>
public sealed record ProductSnapshotRow(
    string Sku,
    string Name,
    string? Description,
    string CategoryId,
    string? Brand,
    double PriceAmount,
    string PriceCurrency,
    string Status,
    string? Size,
    string? Color,
    IReadOnlyDictionary<string, object?> ExtendedAttributes);
