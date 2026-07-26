namespace Kart.Search.Domain.SearchDocuments;

/// <summary>
/// <c>{ avg, count }</c> stored on <see cref="SearchDocument"/> - always recomputed deterministically
/// from the full <c>perReviewRatings</c> map (kept in the separate <c>search_rating_ledger</c> index,
/// database-design.md), never incrementally mutated. Neither <c>ReviewSubmitted</c> nor
/// <c>ReviewUpdated</c> carries an ordering/version field, so a running-total delta approach cannot
/// be made idempotent against RabbitMQ's at-least-once redelivery the way the catalog events'
/// <c>occurredAt</c>-guarded approach can (ddd-model.md's Invariants) - recomputing from the map's
/// current contents makes every write idempotent by construction instead.
/// </summary>
public sealed record RatingSignal(double Avg, int Count)
{
    public static readonly RatingSignal None = new(0, 0);

    /// <summary>Deterministic recompute from the full per-reviewId ledger - <c>avg = mean(values)</c>,
    /// <c>count = size(map)</c> (ddd-model.md). Called after every accepted ReviewSubmitted/ReviewUpdated.</summary>
    public static RatingSignal FromReviews(IReadOnlyDictionary<string, double> perReviewRatings)
    {
        if (perReviewRatings.Count == 0)
        {
            return None;
        }

        return new RatingSignal(perReviewRatings.Values.Average(), perReviewRatings.Count);
    }
}
