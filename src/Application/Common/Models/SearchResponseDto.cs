namespace Kart.Search.Application.Common.Models;

/// <summary>Mirrors <c>api-contract.yaml</c>'s <c>CategoryRef</c> schema exactly.</summary>
public sealed record CategoryRefDto(string CategoryId, string? CategoryName);

/// <summary>Mirrors <c>api-contract.yaml</c>'s <c>Money</c> schema exactly.</summary>
public sealed record MoneyDto(double Amount, string Currency);

/// <summary>Mirrors <c>api-contract.yaml</c>'s <c>RatingSummary</c> schema exactly.</summary>
public sealed record RatingSummaryDto(double Avg, int Count);

/// <summary>Mirrors <c>api-contract.yaml</c>'s <c>SearchResultItem</c> schema exactly.
/// <see cref="Availability"/> is always <c>"Active"</c> - Discontinued documents never reach here
/// (excluded from default result sets before this DTO is built).</summary>
public sealed record SearchResultItemDto(
    string Sku,
    string Name,
    string? Description,
    string? Brand,
    CategoryRefDto Category,
    MoneyDto Price,
    string Availability,
    RatingSummaryDto Rating,
    string? Size,
    string? Color,
    string? ImageUrl = null);

/// <summary>Mirrors <c>api-contract.yaml</c>'s <c>FacetBucket</c> schema.</summary>
public sealed record FacetBucketDto(string Value, long Count);

/// <summary>Mirrors <c>api-contract.yaml</c>'s <c>Facets</c> schema - aggregated over the exact
/// same filtered result set the accompanying <c>results</c>/<c>pagination</c> reflect
/// (requirement-spec.md's Domain Invariant), never a separately-cached aggregate.</summary>
public sealed record FacetsDto(
    IReadOnlyList<FacetBucketDto> Category,
    IReadOnlyList<FacetBucketDto> Price,
    IReadOnlyList<FacetBucketDto> Rating);

/// <summary>Mirrors <c>api-contract.yaml</c>'s <c>Pagination</c> schema.</summary>
public sealed record PaginationDto(int Page, int Size, long TotalHits, bool TotalHitsIsApproximate);

/// <summary>Mirrors <c>api-contract.yaml</c>'s <c>SearchResponse</c> schema - the entire
/// <c>GET /v1/search</c> success envelope. No generic wrapper (kart-conventions.md: success
/// responses are the endpoint's own strongly-typed shape; only errors use the platform's
/// <c>ProblemDetails</c> envelope).</summary>
public sealed record SearchResponseDto(
    IReadOnlyList<SearchResultItemDto> Results,
    FacetsDto Facets,
    PaginationDto Pagination,
    bool Truncated,
    IReadOnlyList<string> DegradedFacets);
