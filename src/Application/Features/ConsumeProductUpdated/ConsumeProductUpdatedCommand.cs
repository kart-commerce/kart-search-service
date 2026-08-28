using Kart.Search.Domain.SearchDocuments;
using MediatR;

namespace Kart.Search.Application.Features.ConsumeProductUpdated;

/// <summary>SRCH-3: guarded catalog-field update - only members named in Product's own
/// <c>changedFields</c> are non-null (ddd-model.md). <see cref="CategoryId"/> non-null means the
/// category itself changed, triggering a fresh <c>CategoryLookup</c> resolution.</summary>
public sealed record ConsumeProductUpdatedCommand(
    string Sku,
    string? Name,
    string? Description,
    string? CategoryId,
    string? Brand,
    FacetableAttributes? Attributes,
    DateTimeOffset OccurredAt,
    string? ImageUrl = null) : IRequest;
