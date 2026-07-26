using MediatR;

namespace Kart.Search.Application.Features.RebuildSearchIndex;

/// <summary>
/// SRCH-9: blue-green reindex via alias swap (design-decisions.md's "Index Rebuild Strategy",
/// edge-cases.md "Index Rebuild During High Write Volume"). Operator-triggered only - exposed via
/// an internal-only endpoint (<c>POST /internal/reindex</c>), not part of <c>api-contract.yaml</c>
/// (no client-facing contract exists for this operation).
/// </summary>
public sealed record RebuildSearchIndexCommand : IRequest<RebuildSearchIndexResponse>;

public sealed record RebuildSearchIndexResponse(string NewIndexName, long DocumentsIndexed);
