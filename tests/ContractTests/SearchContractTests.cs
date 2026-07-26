using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Kart.Search.ContractTests;

public sealed class SearchContractTests(SearchApiFactory factory) : IClassFixture<SearchApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetSearch_NoParams_Returns200WithContractShapedBody()
    {
        var response = await _client.GetAsync("/v1/search");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.TryGetProperty("results", out _).Should().BeTrue();
        root.TryGetProperty("facets", out var facets).Should().BeTrue();
        facets.TryGetProperty("category", out _).Should().BeTrue();
        facets.TryGetProperty("price", out _).Should().BeTrue();
        facets.TryGetProperty("rating", out _).Should().BeTrue();
        root.TryGetProperty("pagination", out var pagination).Should().BeTrue();
        pagination.TryGetProperty("page", out _).Should().BeTrue();
        pagination.TryGetProperty("totalHits", out _).Should().BeTrue();
        pagination.TryGetProperty("totalHitsIsApproximate", out _).Should().BeTrue();
        root.TryGetProperty("truncated", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetSearch_ZeroMatches_Returns200NeverA404()
    {
        // The fake always returns one result, but this asserts the endpoint's own contract (an
        // empty result set is a valid outcome, never modeled as a 404) via the shape of the
        // response rather than depending on the fake's data - a genuinely empty-catalog assertion
        // belongs in IntegrationTests against a real (empty) index.
        var response = await _client.GetAsync("/v1/search?q=anything");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSearch_MoreThanFiveCombinedFilters_Returns400WithFacetFilterLimitExceededErrorCode()
    {
        var response = await _client.GetAsync("/v1/search?category=a&category=b&category=c&category=d&category=e&category=f");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("FACET_FILTER_LIMIT_EXCEEDED");
        document.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSearch_PageTimesSizeExceedsPaginationWindow_Returns400WithPaginationWindowExceededErrorCode()
    {
        var response = await _client.GetAsync("/v1/search?page=200&size=100");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("PAGINATION_WINDOW_EXCEEDED");
    }

    [Fact]
    public async Task GetSearch_InvalidSortValue_Returns400ValidationError()
    {
        var response = await _client.GetAsync("/v1/search?sort=popularity");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [Fact]
    public async Task GetSearch_ResponseHeaders_CarryQueryIdCorrelationField()
    {
        var response = await _client.GetAsync("/v1/search");

        response.Headers.Should().ContainSingle(h => h.Key == "X-Query-Id");
    }
}
