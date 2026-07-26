using Kart.Search.Application.Features.RebuildSearchIndex;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kart.Search.Api.Controllers;

/// <summary>
/// Operator-only endpoints - deliberately NOT part of <c>api-contract.yaml</c> (no client-facing
/// contract exists for the blue-green rebuild operation, per architecture.md/database-design.md;
/// this is purely an operational trigger, not a feature for Client/Web/Mobile).
/// </summary>
[ApiController]
[Route("internal")]
public sealed class InternalController(ISender sender) : ControllerBase
{
    /// <summary>SRCH-9: triggers a blue-green reindex (design-decisions.md's "Index Rebuild
    /// Strategy"). Long-running - runs to completion synchronously within this request; an
    /// operator invoking this is expected to tolerate a long-lived HTTP call (or run it via a
    /// script with a generous timeout), since this is an infrequent, off-request-path
    /// operation, not a feature the request-serving path depends on.</summary>
    [HttpPost("reindex")]
    [ProducesResponseType(typeof(RebuildSearchIndexResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RebuildSearchIndexResponse>> Reindex(CancellationToken cancellationToken)
    {
        var response = await sender.Send(new RebuildSearchIndexCommand(), cancellationToken);
        return Ok(response);
    }
}
