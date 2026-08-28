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
        var applied = await categoryLookupRepository.UpsertAsync(request.CategoryId, request.Name, request.OccurredAt, cancellationToken);

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
