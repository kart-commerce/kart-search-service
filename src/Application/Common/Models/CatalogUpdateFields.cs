using Kart.Search.Domain.SearchDocuments;

namespace Kart.Search.Application.Common.Models;

/// <summary>The subset of fields a <c>ProductUpdated</c> event's own <c>changedFields</c> named -
/// a <c>null</c> member means "not changed" (ddd-model.md), never an explicit clear. Passed to
/// <see cref="Kart.Search.Application.Common.Interfaces.ISearchProjectionRepository.ApplyCatalogUpdateAsync"/>
/// so the guarded OpenSearch script only <c>$set</c>s the fields that actually changed.</summary>
public sealed record CatalogUpdateFields(
    string? Name,
    string? Description,
    string? CategoryId,
    string? CategoryName,
    string? Brand,
    FacetableAttributes? Attributes,
    string? ImageUrl = null);
