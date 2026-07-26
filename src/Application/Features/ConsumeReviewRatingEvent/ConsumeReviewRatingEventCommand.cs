using MediatR;

namespace Kart.Search.Application.Features.ConsumeReviewRatingEvent;

/// <summary>
/// SRCH-6: shared by both <c>ReviewSubmitted</c> and <c>ReviewUpdated</c> - maintains the rating
/// ledger and the recomputed <c>SearchDocument.rating</c> field only (database-design.md).
///
/// <see cref="LedgerKey"/> is <c>orderId</c>, not <c>reviewId</c> - see
/// <see cref="Kart.Search.Application.Common.Interfaces.IRatingLedgerRepository"/>'s remarks for
/// why: <c>ReviewUpdated</c>'s actual wire payload carries no <c>reviewId</c>, so both events key
/// the per-review ledger map by <c>orderId</c> instead, which both events do carry and which
/// uniquely identifies one review instance for a given sku.
/// </summary>
public sealed record ConsumeReviewRatingEventCommand(string Sku, string LedgerKey, double Rating) : IRequest;
