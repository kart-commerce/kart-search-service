using Kart.Search.Application.Common.Models;
using Kart.Search.Application.Features.SearchProducts;
using Kart.Shared.Observability;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Api.Controllers;

/// <summary><c>GET /v1/search</c> (api-contract.yaml) - the entire public surface of this service.
/// Query-only, no mutating endpoint of any kind (requirement-spec.md §1).</summary>
[ApiController]
[Route("v1/search")]
public sealed class SearchController(ISender sender, ILogger<SearchController> logger) : ControllerBase
{
    private const string FlowName = "NormalShoppingPurchaseJourney";

    [HttpGet]
    [ProducesResponseType(typeof(SearchResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchResponseDto>> Search(
        [FromQuery] string? q,
        [FromQuery] IReadOnlyList<string>? category,
        [FromQuery] double? priceMin,
        [FromQuery] double? priceMax,
        [FromQuery] double? ratingMin,
        [FromQuery] string sort = "relevance",
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        using var flowScope = KartFlowContext.Push(FlowName);
        logger.LogInformation("Stage {Stage}: search request received (q={Query}, page={Page})", "SearchRequestReceived", q, page);

        var query = new SearchProductsQuery(q, category, priceMin, priceMax, ratingMin, sort, page, size);
        var response = await sender.Send(query, cancellationToken);

        logger.LogInformation("Stage {Stage}: search returned {Count} result(s)", "SearchResultsReturned", response.Results.Count);
        return Ok(response);
    }
}
