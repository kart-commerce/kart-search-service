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

        var fields = new CatalogUpdateFields(request.Name, request.Description, request.CategoryId, categoryName, request.Brand, request.Attributes);
        var applied = await projectionRepository.ApplyCatalogUpdateAsync(request.Sku, fields, request.OccurredAt, cancellationToken);

        if (applied)
        {
            logger.LogInformation("Applied catalog update for {Sku}", request.Sku);
        }
        else
        {
            logger.LogInformation("Rejected stale-ordered ProductUpdated for {Sku} (occurredAt {OccurredAt})", request.Sku, request.OccurredAt);
        }
    }
}
