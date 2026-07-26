using System.Text.Json;
using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;
using Kart.Search.Domain.SearchDocuments;
using Microsoft.Extensions.Options;

namespace Kart.Search.Infrastructure.Search;

/// <summary>
/// SRCH-7/SRCH-8: <c>GET /v1/search</c>'s query side. Ranking (<c>ddd-model.md</c>'s
/// <c>RankingProfile</c>) is a single-pass OpenSearch <c>function_score</c> query with a Painless
/// <c>script_score</c> whose params are sourced directly from
/// <see cref="Kart.Search.Domain.SearchDocuments.RankingProfile"/>'s constants (never re-typed as
/// separate literals here). Facet aggregation runs in the same request, over the exact same
/// filtered query context the hits reflect (requirement-spec's Domain Invariant) - never a
/// separately-cached aggregate. Query-timeout graceful degradation (edge-cases.md): a 300ms
/// server-side timeout, and on <c>timed_out</c>, one retry per facet dropped, highest-cardinality
/// first (<c>category</c>, then <c>price</c>, then <c>rating</c>).
/// </summary>
public sealed class OpenSearchSearchRepository(OpenSearchHttpClient client, IOptions<OpenSearchOptions> options)
    : ISearchQueryRepository
{
    private static readonly TimeSpan ServerTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ClientTimeoutBudget = TimeSpan.FromMilliseconds(400);
    private const int MaxBucketsPerFacet = 100;
    private static readonly string[] FacetDropOrder = ["category", "price", "rating"];

    public async Task<SearchResponseDto> SearchAsync(SearchQueryCriteria criteria, CancellationToken cancellationToken)
    {
        var degradedFacets = new List<string>();
        var facetsToInclude = new HashSet<string>(["category", "price", "rating"]);

        JsonDocument response;
        while (true)
        {
            var body = BuildSearchBody(criteria, facetsToInclude);
            response = await client.SearchAsync(options.Value.ProductsAlias, body, ClientTimeoutBudget, cancellationToken);

            var timedOut = response.RootElement.TryGetProperty("timed_out", out var timedOutElement) && timedOutElement.GetBoolean();
            if (!timedOut || facetsToInclude.Count == 0)
            {
                break;
            }

            var nextToDrop = FacetDropOrder.FirstOrDefault(facetsToInclude.Contains);
            if (nextToDrop is null)
            {
                break;
            }

            facetsToInclude.Remove(nextToDrop);
            degradedFacets.Add(nextToDrop);
        }

        return ParseResponse(response, criteria, degradedFacets);
    }

    private static object BuildSearchBody(SearchQueryCriteria criteria, HashSet<string> facetsToInclude)
    {
        var filters = new List<object> { new { term = new { availability = "Active" } } };

        if (criteria.CategoryIds.Count > 0)
        {
            filters.Add(new { terms = new Dictionary<string, object> { ["category.categoryId"] = criteria.CategoryIds } });
        }

        if (criteria.PriceMin.HasValue || criteria.PriceMax.HasValue)
        {
            var range = new Dictionary<string, object>();
            if (criteria.PriceMin.HasValue)
            {
                range["gte"] = criteria.PriceMin.Value;
            }

            if (criteria.PriceMax.HasValue)
            {
                range["lte"] = criteria.PriceMax.Value;
            }

            filters.Add(new { range = new Dictionary<string, object> { ["price.amount"] = range } });
        }

        if (criteria.RatingMin.HasValue)
        {
            filters.Add(new { range = new Dictionary<string, object> { ["rating.avg"] = new { gte = criteria.RatingMin.Value } } });
        }

        // The exact-SKU/brand boost clauses must be able to surface a match on their own (a query
        // for a literal SKU rarely also matches name/description/brand text) - so multi_match and
        // the exact-match clauses are siblings under "should" with minimum_should_match: 1, not a
        // hard "must" (multi_match) plus score-only "should" boosts, which would make an exact-SKU
        // search return zero hits whenever the SKU string itself never appears in name/description.
        object boolQueryBody;
        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var should = new List<object>
            {
                new { multi_match = new { query = criteria.Query, fields = new[] { "name", "description", "brand" } } },
                new { term = new Dictionary<string, object> { ["sku"] = new { value = criteria.Query, boost = 5 } } },
                new { term = new Dictionary<string, object> { ["brand.keyword"] = new { value = criteria.Query, boost = 3 } } },
            };

            boolQueryBody = new { filter = filters, should, minimum_should_match = 1 };
        }
        else
        {
            // No free-text query - browse-all, filtered/sorted only (api-contract.yaml). A bool
            // query with only a filter clause already matches every document passing it.
            boolQueryBody = new { filter = filters };
        }

        object boolQuery = new { @bool = boolQueryBody };

        object query = criteria.Sort == SearchSort.Relevance
            ? new
            {
                function_score = new
                {
                    query = boolQuery,
                    script_score = new
                    {
                        script = new
                        {
                            lang = "painless",
                            source = """
                                double textNorm = _score / (_score + params.k);
                                double ratingAvg = doc['rating.avg'].size() > 0 ? doc['rating.avg'].value : 0.0;
                                long ratingCount = doc['rating.count'].size() > 0 ? (long) doc['rating.count'].value : 0L;
                                double ratingComp = ratingCount >= params.ratingThreshold ? (ratingAvg / 5.0) : params.neutralRating;
                                double sponsoredBoost = (doc['sponsored'].size() > 0 && doc['sponsored'].value) ? params.sponsoredBoost : 0.0;
                                return (textNorm * params.textWeight) + (ratingComp * params.ratingWeight) + params.inStockWeight + sponsoredBoost;
                                """,
                            @params = new
                            {
                                k = RankingProfile.TextNormalizationK,
                                ratingThreshold = RankingProfile.RatingCountThreshold,
                                neutralRating = RankingProfile.NeutralRatingComponent,
                                textWeight = RankingProfile.TextRelevanceWeight,
                                ratingWeight = RankingProfile.RatingWeight,
                                inStockWeight = RankingProfile.InStockWeight,
                                sponsoredBoost = RankingProfile.SponsoredBoost,
                            },
                        },
                    },
                    boost_mode = "replace",
                },
            }
            : boolQuery;

        var aggs = new Dictionary<string, object>();
        if (facetsToInclude.Contains("category"))
        {
            aggs["category"] = new { terms = new { field = "category.categoryId", size = MaxBucketsPerFacet } };
        }

        if (facetsToInclude.Contains("price"))
        {
            aggs["price"] = new
            {
                range = new
                {
                    field = "price.amount",
                    ranges = new object[]
                    {
                        new { key = "0-25", to = 25 },
                        new { key = "25-50", from = 25, to = 50 },
                        new { key = "50-100", from = 50, to = 100 },
                        new { key = "100-250", from = 100, to = 250 },
                        new { key = "250-500", from = 250, to = 500 },
                        new { key = "500-1000", from = 500, to = 1000 },
                        new { key = "1000+", from = 1000 },
                    },
                },
            };
        }

        if (facetsToInclude.Contains("rating"))
        {
            aggs["rating"] = new
            {
                range = new
                {
                    field = "rating.avg",
                    ranges = new object[]
                    {
                        new { key = "1", from = 1 },
                        new { key = "2", from = 2 },
                        new { key = "3", from = 3 },
                        new { key = "4", from = 4 },
                        new { key = "5", from = 5 },
                    },
                },
            };
        }

        var body = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["from"] = (criteria.Page - 1) * criteria.Size,
            ["size"] = criteria.Size,
            ["timeout"] = $"{(int)ServerTimeout.TotalMilliseconds}ms",
            ["track_total_hits"] = 10_000,
            ["aggs"] = aggs,
        };

        // sort=relevance bypasses this entirely (native ordering already comes from the
        // function_score query above) - the other three are literal single-field sorts.
        if (criteria.Sort != SearchSort.Relevance)
        {
            body["sort"] = criteria.Sort switch
            {
                SearchSort.PriceAsc => new object[] { new Dictionary<string, object> { ["price.amount"] = "asc" } },
                SearchSort.PriceDesc => new object[] { new Dictionary<string, object> { ["price.amount"] = "desc" } },
                SearchSort.RatingDesc => new object[] { new Dictionary<string, object> { ["rating.avg"] = "desc" } },
                _ => Array.Empty<object>(),
            };
        }

        return body;
    }

    private static SearchResponseDto ParseResponse(JsonDocument response, SearchQueryCriteria criteria, List<string> degradedFacets)
    {
        var root = response.RootElement;
        var hitsElement = root.GetProperty("hits");

        var results = new List<SearchResultItemDto>();
        foreach (var hit in hitsElement.GetProperty("hits").EnumerateArray())
        {
            results.Add(ToResultItem(hit.GetProperty("_source")));
        }

        var totalElement = hitsElement.GetProperty("total");
        var totalHits = totalElement.GetProperty("value").GetInt64();
        var isApproximate = totalElement.TryGetProperty("relation", out var relation) && relation.GetString() == "gte";

        var facets = new FacetsDto(
            ReadTermsFacet(root, "category"),
            ReadRangeFacet(root, "price"),
            ReadRangeFacet(root, "rating"));

        return new SearchResponseDto(
            results,
            facets,
            new PaginationDto(criteria.Page, criteria.Size, totalHits, isApproximate),
            degradedFacets.Count > 0,
            degradedFacets);
    }

    private static SearchResultItemDto ToResultItem(JsonElement source)
    {
        var category = source.GetProperty("category");
        var price = source.GetProperty("price");
        var rating = source.GetProperty("rating");

        return new SearchResultItemDto(
            source.GetProperty("sku").GetString()!,
            source.GetProperty("name").GetString()!,
            GetNullableString(source, "description"),
            GetNullableString(source, "brand"),
            new CategoryRefDto(category.GetProperty("categoryId").GetString()!, GetNullableString(category, "categoryName")),
            new MoneyDto(price.GetProperty("amount").GetDouble(), price.GetProperty("currency").GetString()!),
            "Active",
            new RatingSummaryDto(rating.GetProperty("avg").GetDouble(), rating.GetProperty("count").GetInt32()),
            GetNullableString(source, "size"),
            GetNullableString(source, "color"));
    }

    private static string? GetNullableString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;

    private static IReadOnlyList<FacetBucketDto> ReadTermsFacet(JsonElement root, string name)
    {
        if (!root.TryGetProperty("aggregations", out var aggregations) || !aggregations.TryGetProperty(name, out var facet))
        {
            return [];
        }

        return facet.GetProperty("buckets").EnumerateArray()
            .Select(bucket => new FacetBucketDto(bucket.GetProperty("key").ToString(), bucket.GetProperty("doc_count").GetInt64()))
            .ToList();
    }

    private static IReadOnlyList<FacetBucketDto> ReadRangeFacet(JsonElement root, string name)
    {
        if (!root.TryGetProperty("aggregations", out var aggregations) || !aggregations.TryGetProperty(name, out var facet))
        {
            return [];
        }

        return facet.GetProperty("buckets").EnumerateArray()
            .Select(bucket => new FacetBucketDto(bucket.GetProperty("key").GetString()!, bucket.GetProperty("doc_count").GetInt64()))
            .ToList();
    }
}
