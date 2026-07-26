using Kart.Search.Domain.SearchDocuments;

namespace Kart.Search.Application.Common.Interfaces;

/// <summary>
/// Backs the <c>search_rating_ledger</c> index (SRCH-6, database-design.md) - the write-side-only
/// per-review bookkeeping <c>RatingSignal</c> is recomputed from, never queried directly.
///
/// <see cref="ApplyRatingAsync"/>'s <paramref name="ledgerKey"><c>ledgerKey</c></paramref> is this
/// build's own resolved ambiguity: ddd-model.md's stated mechanism keys the per-review map by
/// <c>reviewId</c>, but event-contract.md's actual <c>ReviewUpdated</c> payload
/// (<c>orderId, sku, oldRating, newRating</c>) carries no <c>reviewId</c> - only <c>ReviewSubmitted</c>
/// does. Since both events carry <c>orderId</c>+<c>sku</c>, and one order yields at most one review
/// for a given sku, this service keys the ledger by <c>orderId</c> for BOTH events (not
/// <c>reviewId</c>) - using the same key for both is what preserves the idempotent-overwrite
/// guarantee the mechanism relies on; keying Submitted by reviewId and Updated by orderId would
/// silently create two ledger entries for what is really one review.
/// </summary>
public interface IRatingLedgerRepository
{
    /// <summary>Upserts <c>perReviewRatings[ledgerKey] = rating</c> (idempotent - a redelivery of
    /// the same key/rating pair is a no-op in effect) and returns the freshly recomputed
    /// <see cref="RatingSignal"/> (<c>avg = mean(values)</c>, <c>count = size(map)</c>) from the
    /// ledger's current contents.</summary>
    Task<RatingSignal> ApplyRatingAsync(string sku, string ledgerKey, double rating, CancellationToken cancellationToken);
}
