using Kart.Search.Application.Common.Exceptions;
using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;
using MediatR;

namespace Kart.Search.Application.Features.SearchProducts;

public sealed class SearchProductsQueryHandler(ISearchQueryRepository searchQueryRepository)
    : IRequestHandler<SearchProductsQuery, SearchResponseDto>
{
    private const int MaxCombinedFilters = 5;
    private const int MaxResultWindow = 10_000;

    public Task<SearchResponseDto> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var categoryIds = request.Category ?? [];

        // edge-cases.md "Query Timeout Under High-Cardinality Filters": max 5 concurrent filter
        // VALUES combined across category[]/priceMin/priceMax/ratingMin - each distinct categoryId
        // counts as one filter value (api-contract.yaml).
        var filterCount = categoryIds.Count
            + (request.PriceMin.HasValue ? 1 : 0)
            + (request.PriceMax.HasValue ? 1 : 0)
            + (request.RatingMin.HasValue ? 1 : 0);

        if (filterCount > MaxCombinedFilters)
        {
            throw new FacetFilterLimitExceededException(filterCount);
        }

        if ((long)request.Page * request.Size > MaxResultWindow)
        {
            throw new PaginationWindowExceededException(request.Page, request.Size);
        }

        var sort = request.Sort switch
        {
            "price_asc" => SearchSort.PriceAsc,
            "price_desc" => SearchSort.PriceDesc,
            "rating_desc" => SearchSort.RatingDesc,
            _ => SearchSort.Relevance,
        };

        var criteria = new SearchQueryCriteria(request.Q, categoryIds, request.PriceMin, request.PriceMax, request.RatingMin, sort, request.Page, request.Size);

        return searchQueryRepository.SearchAsync(criteria, cancellationToken);
    }
}
