namespace Kart.Search.Application.Common.Exceptions;

/// <summary>api-contract.yaml's <c>FACET_FILTER_LIMIT_EXCEEDED</c> - more than 5 combined filter
/// values (category[] + priceMin + priceMax + ratingMin) on one query (edge-cases.md "Query
/// Timeout Under High-Cardinality Filters").</summary>
public sealed class FacetFilterLimitExceededException(int filterCount)
    : Exception($"Query supplied {filterCount} combined filter values; the maximum is 5.")
{
    public int FilterCount { get; } = filterCount;
}
