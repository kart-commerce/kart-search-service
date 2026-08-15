using Kart.Search.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Application.Features.ConsumeCategoryUpdated;

public sealed class ConsumeCategoryUpdatedCommandHandler(
    ICategoryLookupRepository categoryLookupRepository,
    ILogger<ConsumeCategoryUpdatedCommandHandler> logger) : IRequestHandler<ConsumeCategoryUpdatedCommand>
{
    public async Task Handle(ConsumeCategoryUpdatedCommand request, CancellationToken cancellationToken)
    {
        // Checkpoint-logging taxonomy stage 11 (ReadModelWriteStarted/Persisted) - CategoryLookup
        // is a denormalized read-model too (consulted when (re)indexing products), same as the
        // SearchDocument index write (checkpoint-logging-standard.md).
        logger.LogInformation("Stage {Stage}: applying CategoryLookup update for {CategoryId}", "CategoryLookupWriteStarted", request.CategoryId);
        var applied = await categoryLookupRepository.UpsertAsync(request.CategoryId, request.Name, request.OccurredAt, cancellationToken);

        // Checkpoint-logging taxonomy stage 5 (DecisionBranch) - applied-in-order vs
        // rejected-as-stale are two meaningfully different, separately greppable outcomes.
        if (applied)
        {
            logger.LogInformation("Stage {Stage}: updated CategoryLookup for {CategoryId}", "CategoryLookupPersisted", request.CategoryId);
        }
        else
        {
            logger.LogInformation("Stage {Stage}: rejected stale-ordered CategoryUpdated for {CategoryId} (occurredAt {OccurredAt})", "CategoryLookupWriteRejectedStaleOrder", request.CategoryId, request.OccurredAt);
        }
    }
}
