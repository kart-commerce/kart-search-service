using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Features.ConsumeReviewRatingEvent;
using Kart.Search.Domain.SearchDocuments;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Search.UnitTests.Features.ConsumeReviewRatingEvent;

public sealed class ConsumeReviewRatingEventCommandHandlerTests
{
    private readonly Mock<IRatingLedgerRepository> _ratingLedgerRepository = new();
    private readonly Mock<ISearchProjectionRepository> _projectionRepository = new();

    private ConsumeReviewRatingEventCommandHandler CreateHandler() =>
        new(_ratingLedgerRepository.Object, _projectionRepository.Object, NullLogger<ConsumeReviewRatingEventCommandHandler>.Instance);

    [Fact]
    public async Task Handle_AppliesRecomputedRatingFromLedgerOntoProjection()
    {
        var recomputed = new RatingSignal(4.5, 2);
        _ratingLedgerRepository
            .Setup(r => r.ApplyRatingAsync("SKU-1", "order-1", 5.0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recomputed);

        await CreateHandler().Handle(new ConsumeReviewRatingEventCommand("SKU-1", "order-1", 5.0), CancellationToken.None);

        _projectionRepository.Verify(r => r.ApplyRatingAsync("SKU-1", recomputed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RedeliveryOfSameLedgerKeyIsIdempotentNoOp()
    {
        // Both ReviewSubmitted and a redelivered ReviewUpdated for the same review key by
        // construction (ddd-model.md) - overwriting perReviewRatings[ledgerKey] twice with the
        // same value never double-counts, since avg/count are recomputed from the full map, not
        // an incremental delta.
        var recomputed = new RatingSignal(5.0, 1);
        _ratingLedgerRepository
            .Setup(r => r.ApplyRatingAsync("SKU-1", "order-1", 5.0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recomputed);

        var handler = CreateHandler();
        await handler.Handle(new ConsumeReviewRatingEventCommand("SKU-1", "order-1", 5.0), CancellationToken.None);
        await handler.Handle(new ConsumeReviewRatingEventCommand("SKU-1", "order-1", 5.0), CancellationToken.None);

        _projectionRepository.Verify(r => r.ApplyRatingAsync("SKU-1", It.Is<RatingSignal>(sig => sig.Count == 1), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
