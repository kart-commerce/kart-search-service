using Kart.Search.Application.Common.Models;

namespace Kart.Search.Application.Common.Interfaces;

/// <summary>The query side (SRCH-7/SRCH-8) - the only inbound synchronous dependency this service
/// has, per architecture.md's Distributed-Monolith Risk assessment: queries OpenSearch only, never
/// calls out to another service. Implemented by
/// <c>Infrastructure/Search/OpenSearchSearchRepository</c> via a single-pass <c>function_score</c>
/// query (ddd-model.md's RankingProfile) plus facet aggregations, with the 300ms timeout/
/// graceful-degradation behavior edge-cases.md mandates.</summary>
public interface ISearchQueryRepository
{
    Task<SearchResponseDto> SearchAsync(SearchQueryCriteria criteria, CancellationToken cancellationToken);
}
