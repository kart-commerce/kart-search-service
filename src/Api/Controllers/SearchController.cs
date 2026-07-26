using Kart.Search.Application.Common.Models;
using Kart.Search.Application.Features.SearchProducts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kart.Search.Api.Controllers;

/// <summary><c>GET /v1/search</c> (api-contract.yaml) - the entire public surface of this service.
/// Query-only, no mutating endpoint of any kind (requirement-spec.md §1).</summary>
[ApiController]
[Route("v1/search")]
public sealed class SearchController(ISender sender) : ControllerBase
{
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
        var query = new SearchProductsQuery(q, category, priceMin, priceMax, ratingMin, sort, page, size);
        var response = await sender.Send(query, cancellationToken);
        return Ok(response);
    }
}
