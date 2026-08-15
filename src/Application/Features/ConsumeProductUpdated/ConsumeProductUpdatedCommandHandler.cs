using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Application.Features.ConsumeProductUpdated;

public sealed class ConsumeProductUpdatedCommandHandler(
    ISearchProjectionRepository projectionRepository,
    ICategoryLookupRepository categoryLookupRepository,
    ILogger<ConsumeProductUpdatedCommandHandler> logger) : IRequestHandler<ConsumeProductUpdatedCommand>
{
    public async Task Handle(ConsumeProductUpdatedCommand request, CancellationToken cancellationToken)
    {
        // Re-resolve category.categoryName only when changedFields included the category
        // (ddd-model.md) - never from a Product-event field, since Product's write side has no
        // such column.
        string? categoryName = null;
        if (request.CategoryId is not null)
        {
            categoryName = await categoryLookupRepository.GetCategoryNameAsync(request.CategoryId, cancellationToken);
        }

        var fields = new CatalogUpdateFields(request.Name, request.Description, request.CategoryId, categoryName, request.Brand, request.Attributes, request.ImageUrl);

        // Checkpoint-logging taxonomy stage 11 (ReadModelWriteStarted/Persisted) - the search
        // index write IS this service's read-model write (checkpoint-logging-standard.md).
        logger.LogInformation("Stage {Stage}: applying catalog update for {Sku}", "SearchIndexWriteStarted", request.Sku);
        var applied = await projectionRepository.ApplyCatalogUpdateAsync(request.Sku, fields, request.OccurredAt, cancellationToken);

        // Checkpoint-logging taxonomy stage 5 (DecisionBranch) - applied-in-order vs
        // rejected-as-stale are two meaningfully different, separately greppable outcomes.
        if (applied)
        {
            logger.LogInformation("Stage {Stage}: applied catalog update for {Sku}", "SearchIndexPersisted", request.Sku);
        }
        else
        {
            logger.LogInformation("Stage {Stage}: rejected stale-ordered ProductUpdated for {Sku} (occurredAt {OccurredAt})", "SearchIndexWriteRejectedStaleOrder", request.Sku, request.OccurredAt);
        }
    }
}
