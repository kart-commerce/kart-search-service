namespace Kart.Search.Domain.SearchDocuments;

/// <summary>
/// <c>categoryId</c> comes from Product's own events; <c>categoryName</c> is resolved by looking
/// up <see cref="Kart.Search.Domain.CategoryLookups.CategoryLookup"/> at write time - never copied
/// from a Product-event field, since Product's write side has no such column (ddd-model.md,
/// ADR-0018). <c>CategoryName</c> is <c>null</c> only in the narrow window before this service has
/// ever received a <c>CategoryUpdated</c> for this id - never blocks the document from being
/// indexed (ddd-model.md's Cross-Aggregate Interaction).
/// </summary>
public sealed record CategoryRef(string CategoryId, string? CategoryName);
