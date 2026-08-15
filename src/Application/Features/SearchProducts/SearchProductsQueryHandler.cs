using Kart.Search.Application.Common.Exceptions;
using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Application.Features.SearchProducts;

public sealed class SearchProductsQueryHandler(
    ISearchQueryRepository searchQueryRepository,
    ILogger<SearchProductsQueryHandler> logger)
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
            // Checkpoint-logging taxonomy stage 4 (manual guard, not FluentValidation) - the
            // Filter step of the Normal Shopping & Purchase Journey rejecting a too-wide facet
            // combination.
            logger.LogWarning(
                "Stage {Stage}: search rejected, combined filter count {FilterCount} exceeds max {MaxFilters}",
                "FacetFilterLimitExceeded",
                filterCount,
                MaxCombinedFilters);
            throw new FacetFilterLimitExceededException(filterCount);
        }

        if ((long)request.Page * request.Size > MaxResultWindow)
        {
            logger.LogWarning(
                "Stage {Stage}: search rejected, page {Page} size {Size} exceeds max result window {MaxWindow}",
                "PaginationWindowExceeded",
                request.Page,
                request.Size,
                MaxResultWindow);
            throw new PaginationWindowExceededException(request.Page, request.Size);
        }

        var sort = request.Sort switch
        {
            "price_asc" => SearchSort.PriceAsc,
            "price_desc" => SearchSort.PriceDesc,
            "rating_desc" => SearchSort.RatingDesc,
            _ => SearchSort.Relevance,
        };

        // Checkpoint-logging taxonomy stage 5 (DecisionBranch) - the Filter/Sort steps of the
        // Normal Shopping & Purchase Journey resolved to a concrete repository query shape;
        // greppable by Stage without needing the full SearchQueryCriteria payload logged.
        logger.LogInformation(
            "Stage {Stage}: sort={Sort}, filterCount={FilterCount}, hasFreeTextQuery={HasFreeTextQuery}",
            "SearchFilterSortBranchResolved",
            request.Sort,
            filterCount,
            !string.IsNullOrWhiteSpace(request.Q));

        var criteria = new SearchQueryCriteria(request.Q, categoryIds, request.PriceMin, request.PriceMax, request.RatingMin, sort, request.Page, request.Size);

        return searchQueryRepository.SearchAsync(criteria, cancellationToken);
    }
}
