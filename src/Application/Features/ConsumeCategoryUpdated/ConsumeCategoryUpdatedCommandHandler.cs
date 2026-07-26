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
            logger.LogInformation("Updated CategoryLookup for {CategoryId}", request.CategoryId);
        }
        else
        {
            logger.LogInformation("Rejected stale-ordered CategoryUpdated for {CategoryId} (occurredAt {OccurredAt})", request.CategoryId, request.OccurredAt);
        }
    }
}
