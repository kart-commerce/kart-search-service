using Kart.Search.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Kart.Search.Infrastructure.Search;

/// <summary>Backs the <c>CategoryLookup</c> aggregate / <c>search-category-lookup</c> index
/// (SRCH-5). Uses OpenSearch's scripted-upsert form (a <c>script</c> to guard an existing entry,
/// an <c>upsert</c> body for the first-ever <c>CategoryUpdated</c> for a given id) so creation and
/// guarded update share one round-trip instead of a separate exists-check.</summary>
public sealed class OpenSearchCategoryLookupRepository(OpenSearchHttpClient client, IOptions<OpenSearchOptions> options)
    : ICategoryLookupRepository
{
    public async Task<string?> GetCategoryNameAsync(string categoryId, CancellationToken cancellationToken)
    {
        var document = await client.GetAsync(options.Value.CategoryLookupIndex, categoryId, routing: null, cancellationToken);
        if (document is null)
        {
            return null;
        }

        return document.RootElement.GetProperty("_source").GetProperty("categoryName").GetString();
    }

    public async Task<bool> UpsertAsync(string categoryId, string categoryName, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        const string script = """
            Instant incoming = Instant.parse(params.occurredAt);
            Instant stored = ctx._source.lastUpdatedAt != null ? Instant.parse(ctx._source.lastUpdatedAt) : Instant.EPOCH;
            if (incoming.isAfter(stored)) {
              ctx._source.categoryName = params.categoryName;
              ctx._source.lastUpdatedAt = params.occurredAt;
            } else {
              ctx.op = 'noop';
            }
            """;

        var body = new
        {
            script = new
            {
                lang = "painless",
                source = script,
                @params = new { categoryName, occurredAt = OpenSearchDates.ToIso(occurredAt) },
            },
            upsert = new
            {
                categoryId,
                categoryName,
                lastUpdatedAt = OpenSearchDates.ToIso(occurredAt),
            },
        };

        return await client.ScriptedUpdateAsync(options.Value.CategoryLookupIndex, categoryId, body, routing: null, cancellationToken);
    }
}
