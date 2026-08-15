using Kart.Search.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Application.Features.ConsumeProductDiscontinued;

public sealed class ConsumeProductDiscontinuedCommandHandler(
    ISearchProjectionRepository projectionRepository,
    ILogger<ConsumeProductDiscontinuedCommandHandler> logger) : IRequestHandler<ConsumeProductDiscontinuedCommand>
{
    public async Task Handle(ConsumeProductDiscontinuedCommand request, CancellationToken cancellationToken)
    {
        // Checkpoint-logging taxonomy stage 11 (ReadModelWriteStarted/Persisted) - the search
        // index write IS this service's read-model write (checkpoint-logging-standard.md).
        logger.LogInformation("Stage {Stage}: soft-removing {Sku} (discontinued)", "SearchIndexWriteStarted", request.Sku);
        var applied = await projectionRepository.MarkDiscontinuedAsync(request.Sku, request.DiscontinuedAt, cancellationToken);

        // Checkpoint-logging taxonomy stage 5 (DecisionBranch) - applied-in-order vs
        // rejected-as-stale are two meaningfully different, separately greppable outcomes.
        if (applied)
        {
            logger.LogInformation("Stage {Stage}: soft-removed {Sku} (discontinued)", "SearchIndexPersisted", request.Sku);
        }
        else
        {
            logger.LogInformation("Stage {Stage}: rejected stale-ordered ProductDiscontinued for {Sku} (discontinuedAt {DiscontinuedAt})", "SearchIndexWriteRejectedStaleOrder", request.Sku, request.DiscontinuedAt);
        }
    }
}
