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
        var applied = await projectionRepository.ApplyPriceChangeAsync(request.Sku, request.NewPrice, request.OccurredAt, cancellationToken);

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
