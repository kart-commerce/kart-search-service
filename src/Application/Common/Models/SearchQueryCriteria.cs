namespace Kart.Search.Application.Common.Models;

public enum SearchSort
{
    Relevance,
    PriceAsc,
    PriceDesc,
    RatingDesc,
}

/// <summary>The fully-validated set of parameters <c>SearchProductsQueryHandler</c> hands to
/// <see cref="Kart.Search.Application.Common.Interfaces.ISearchQueryRepository"/> - already past
/// the 5-combined-filter and pagination-window caps (SearchProductsQueryValidator).</summary>
public sealed record SearchQueryCriteria(
    string? Query,
    IReadOnlyList<string> CategoryIds,
    double? PriceMin,
    double? PriceMax,
    double? RatingMin,
    SearchSort Sort,
    int Page,
    int Size);
