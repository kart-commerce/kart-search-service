using Kart.Search.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Application.Features.ConsumeProductPriceChanged;

public sealed class ConsumeProductPriceChangedCommandHandler(
    ISearchProjectionRepository projectionRepository,
    ILogger<ConsumeProductPriceChangedCommandHandler> logger) : IRequestHandler<ConsumeProductPriceChangedCommand>
{
    public async Task Handle(ConsumeProductPriceChangedCommand request, CancellationToken cancellationToken)
    {
        // Checkpoint-logging taxonomy stage 11 (ReadModelWriteStarted/Persisted) - the search
        // index write IS this service's read-model write (checkpoint-logging-standard.md).
        logger.LogInformation("Stage {Stage}: applying price change for {Sku}", "SearchIndexWriteStarted", request.Sku);
        var applied = await projectionRepository.ApplyPriceChangeAsync(request.Sku, request.NewPrice, request.OccurredAt, cancellationToken);

        // Checkpoint-logging taxonomy stage 5 (DecisionBranch) - applied-in-order vs
        // rejected-as-stale are two meaningfully different, separately greppable outcomes.
        if (applied)
        {
            logger.LogInformation("Stage {Stage}: applied price change for {Sku}", "SearchIndexPersisted", request.Sku);
        }
        else
        {
            logger.LogInformation("Stage {Stage}: rejected stale-ordered ProductPriceChanged for {Sku} (occurredAt {OccurredAt})", "SearchIndexWriteRejectedStaleOrder", request.Sku, request.OccurredAt);
        }
    }
}
