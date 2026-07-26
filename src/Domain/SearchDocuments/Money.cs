namespace Kart.Search.Domain.SearchDocuments;

/// <summary>Mirrors kart-product-service's own Money value object unchanged in shape
/// (ddd-model.md) - the last value carried on ProductCreated/ProductPriceChanged.</summary>
public sealed record Money(decimal Amount, string Currency);
