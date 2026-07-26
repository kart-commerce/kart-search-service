using Kart.Search.Application.Common.Models;
using MediatR;

namespace Kart.Search.Application.Features.SearchProducts;

/// <summary>SRCH-7/SRCH-8: <c>GET /v1/search</c> - multi-match query with filters, blended
/// ranking, and faceted aggregation (api-contract.yaml). Query-only, read-side of this service's
/// CQRS split - the only inbound synchronous dependency, never calling out to another
/// service.</summary>
public sealed record SearchProductsQuery(
    string? Q,
    IReadOnlyList<string>? Category,
    double? PriceMin,
    double? PriceMax,
    double? RatingMin,
    string Sort,
    int Page,
    int Size) : IRequest<SearchResponseDto>;
