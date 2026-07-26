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
        var applied = await projectionRepository.MarkDiscontinuedAsync(request.Sku, request.DiscontinuedAt, cancellationToken);

        if (applied)
        {
            logger.LogInformation("Soft-removed {Sku} (discontinued)", request.Sku);
        }
        else
        {
            logger.LogInformation("Rejected stale-ordered ProductDiscontinued for {Sku} (discontinuedAt {DiscontinuedAt})", request.Sku, request.DiscontinuedAt);
        }
    }
}
