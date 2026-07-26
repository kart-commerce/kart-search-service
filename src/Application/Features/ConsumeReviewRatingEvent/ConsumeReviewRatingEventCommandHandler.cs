using Kart.Search.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Application.Features.ConsumeReviewRatingEvent;

public sealed class ConsumeReviewRatingEventCommandHandler(
    IRatingLedgerRepository ratingLedgerRepository,
    ISearchProjectionRepository projectionRepository,
    ILogger<ConsumeReviewRatingEventCommandHandler> logger) : IRequestHandler<ConsumeReviewRatingEventCommand>
{
    public async Task Handle(ConsumeReviewRatingEventCommand request, CancellationToken cancellationToken)
    {
        // Idempotent by construction (ddd-model.md): upserting the same ledgerKey/rating pair on
        // redelivery is a no-op in effect, and avg/count are always recomputed from the ledger's
        // full current contents, never mutated incrementally.
        var rating = await ratingLedgerRepository.ApplyRatingAsync(request.Sku, request.LedgerKey, request.Rating, cancellationToken);

        // Field-scoped partial update - touches only rating/lastUpdatedAt, deliberately unguarded
        // by lastCatalogEventAt (disjoint field set, ddd-model.md/database-design.md). A no-op if
        // no SearchDocument exists yet for the sku (a pathological ordering where a review arrives
        // before ProductCreated) - never blocks, per requirement-spec's "never blocks" posture.
        await projectionRepository.ApplyRatingAsync(request.Sku, rating, cancellationToken);

        logger.LogInformation("Applied rating projection for {Sku}: avg={Avg} count={Count}", request.Sku, rating.Avg, rating.Count);
    }
}
