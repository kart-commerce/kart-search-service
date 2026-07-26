using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;

namespace Kart.Search.ContractTests.Fakes;

/// <summary>Wire-shape-only fake (mirrors kart-product-service's own
/// <c>InMemoryProductReadModelRepository</c> ContractTests pattern) - returns a canned but
/// contract-shaped <see cref="SearchResponseDto"/>, never touching a real OpenSearch
/// cluster.</summary>
public sealed class InMemorySearchQueryRepository : ISearchQueryRepository
{
    public Task<SearchResponseDto> SearchAsync(SearchQueryCriteria criteria, CancellationToken cancellationToken)
    {
        var response = new SearchResponseDto(
            Results:
            [
                new SearchResultItemDto(
                    "SKU-1",
                    "Widget",
                    "A widget",
                    "Acme",
                    new CategoryRefDto("cat-1", "Widgets"),
                    new MoneyDto(19.99, "USD"),
                    "Active",
                    new RatingSummaryDto(4.5, 12),
                    "M",
                    "Red"),
            ],
            Facets: new FacetsDto(
                [new FacetBucketDto("cat-1", 1)],
                [new FacetBucketDto("0-25", 1)],
                [new FacetBucketDto("4", 1)]),
            Pagination: new PaginationDto(criteria.Page, criteria.Size, 1, false),
            Truncated: false,
            DegradedFacets: []);

        return Task.FromResult(response);
    }
}
