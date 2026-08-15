using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Domain.SearchDocuments;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Application.Features.ConsumeProductCreated;

public sealed class ConsumeProductCreatedCommandHandler(
    ISearchProjectionRepository projectionRepository,
    ICategoryLookupRepository categoryLookupRepository,
    ILogger<ConsumeProductCreatedCommandHandler> logger,
    TimeProvider timeProvider) : IRequestHandler<ConsumeProductCreatedCommand>
{
    public async Task Handle(ConsumeProductCreatedCommand request, CancellationToken cancellationToken)
    {
        // Cross-Aggregate Interaction (ddd-model.md): a read of CategoryLookup's already-committed
        // state, never a joint write. A missing entry (CategoryUpdated for this category hasn't
        // arrived yet, or never will) leaves categoryName null - never blocks indexing.
        var categoryName = await categoryLookupRepository.GetCategoryNameAsync(request.CategoryId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var document = SearchDocument.Create(
            request.Sku,
            request.Name,
            request.Description,
            request.CategoryId,
            categoryName,
            request.Brand,
            request.Price,
            request.Attributes,
            request.OccurredAt,
            now,
            request.ImageUrl);

        // Checkpoint-logging taxonomy stage 11 (ReadModelWriteStarted/Persisted) - the search
        // index write IS this service's read-model write (checkpoint-logging-standard.md).
        logger.LogInformation("Stage {Stage}: indexing new SearchDocument for {Sku}", "SearchIndexWriteStarted", request.Sku);

        await projectionRepository.CreateAsync(document, cancellationToken);

        logger.LogInformation("Stage {Stage}: indexed new SearchDocument for {Sku}", "SearchIndexPersisted", request.Sku);
    }
}
