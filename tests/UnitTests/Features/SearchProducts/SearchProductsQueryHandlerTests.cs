using FluentAssertions;
using Kart.Search.Application.Common.Exceptions;
using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;
using Kart.Search.Application.Features.SearchProducts;
using Moq;
using Xunit;

namespace Kart.Search.UnitTests.Features.SearchProducts;

public sealed class SearchProductsQueryHandlerTests
{
    private readonly Mock<ISearchQueryRepository> _searchQueryRepository = new();

    private SearchProductsQueryHandler CreateHandler() => new(_searchQueryRepository.Object);

    private static readonly SearchResponseDto EmptyResponse = new([], new FacetsDto([], [], []), new PaginationDto(1, 20, 0, false), false, []);

    [Fact]
    public async Task Handle_MoreThanFiveCombinedFilters_ThrowsFacetFilterLimitExceeded()
    {
        var query = new SearchProductsQuery(null, ["a", "b", "c"], 10, 20, 4, "relevance", 1, 20);

        var act = async () => await CreateHandler().Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<FacetFilterLimitExceededException>();
    }

    [Fact]
    public async Task Handle_ExactlyFiveCombinedFilters_IsAllowed()
    {
        _searchQueryRepository.Setup(r => r.SearchAsync(It.IsAny<SearchQueryCriteria>(), It.IsAny<CancellationToken>())).ReturnsAsync(EmptyResponse);
        var query = new SearchProductsQuery(null, ["a", "b", "c"], 10, 20, null, "relevance", 1, 20);

        var act = async () => await CreateHandler().Handle(query, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_PageTimesSizeExceedsTenThousand_ThrowsPaginationWindowExceeded()
    {
        var query = new SearchProductsQuery(null, [], null, null, null, "relevance", 101, 100);

        var act = async () => await CreateHandler().Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<PaginationWindowExceededException>();
    }

    [Fact]
    public async Task Handle_ValidQuery_BuildsCriteriaAndDelegatesToRepository()
    {
        SearchQueryCriteria? captured = null;
        _searchQueryRepository
            .Setup(r => r.SearchAsync(It.IsAny<SearchQueryCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<SearchQueryCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync(EmptyResponse);

        var query = new SearchProductsQuery("widget", ["cat-1"], 10, 50, 3, "price_desc", 2, 20);
        await CreateHandler().Handle(query, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Sort.Should().Be(SearchSort.PriceDesc);
        captured.Page.Should().Be(2);
        captured.CategoryIds.Should().ContainSingle().Which.Should().Be("cat-1");
    }
}
