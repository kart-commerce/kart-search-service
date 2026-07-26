using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Domain.SearchDocuments;
using Microsoft.Extensions.Options;

namespace Kart.Search.Infrastructure.Search;

/// <summary>Backs <c>search-rating-ledger</c> (SRCH-6, database-design.md) - write-side-only
/// per-review bookkeeping. Upserts <c>perReviewRatings[ledgerKey] = rating</c> (idempotent:
/// redelivery of the same key/rating pair is a no-op in effect) and recomputes
/// <c>avg</c>/<c>count</c> from the ledger's full current contents in the same round-trip, via the
/// update API's <c>?_source=true</c> option.</summary>
public sealed class OpenSearchRatingLedgerRepository(OpenSearchHttpClient client, IOptions<OpenSearchOptions> options)
    : IRatingLedgerRepository
{
    public async Task<RatingSignal> ApplyRatingAsync(string sku, string ledgerKey, double rating, CancellationToken cancellationToken)
    {
        const string script = """
            if (ctx._source.perReviewRatings == null) { ctx._source.perReviewRatings = new HashMap(); }
            ctx._source.perReviewRatings[params.ledgerKey] = params.rating;
            """;

        var body = new
        {
            script = new
            {
                lang = "painless",
                source = script,
                @params = new { ledgerKey, rating },
            },
            upsert = new
            {
                sku,
                perReviewRatings = new Dictionary<string, double> { [ledgerKey] = rating },
            },
        };

        var source = await client.ScriptedUpdateAndGetSourceAsync(options.Value.RatingLedgerIndex, sku, body, cancellationToken);

        if (source is null || !source.Value.TryGetProperty("perReviewRatings", out var ratingsElement))
        {
            return new RatingSignal(rating, 1);
        }

        var ratings = new Dictionary<string, double>();
        foreach (var property in ratingsElement.EnumerateObject())
        {
            ratings[property.Name] = property.Value.GetDouble();
        }

        return RatingSignal.FromReviews(ratings);
    }
}
