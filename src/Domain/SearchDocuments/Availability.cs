namespace Kart.Search.Domain.SearchDocuments;

/// <summary>Mirrors kart-product-service's <c>VariantStatus</c> one-directionally (ddd-model.md):
/// discontinuation is never reversed here either - a reinstated SKU is modeled as a new
/// <c>ProductCreated</c> for what is, from Search's own perspective, a new document.</summary>
public enum Availability
{
    Active,
    Discontinued,
}
