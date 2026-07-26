using Kart.Search.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Application.Features.RebuildSearchIndex;

/// <summary>
/// Blue-green rebuild orchestration (SRCH-9). "Tailing live events during replay" is implemented
/// via <see cref="IRebuildCoordinator"/>: every <c>Consume*CommandHandler</c> best-effort mirrors
/// its write into the shadow index for as long as one is set here, so events arriving during the
/// Postgres backfill below are not lost - there is no RabbitMQ-native event replay to rely on
/// instead (BRD §14).
/// </summary>
public sealed class RebuildSearchIndexCommandHandler(
    ISearchIndexAdmin indexAdmin,
    ICatalogSnapshotReader snapshotReader,
    IRebuildCoordinator rebuildCoordinator,
    ILogger<RebuildSearchIndexCommandHandler> logger,
    TimeSpan? drainGracePeriod = null) : IRequestHandler<RebuildSearchIndexCommand, RebuildSearchIndexResponse>
{
    /// <summary>Grace period after the backfill loop completes, before the alias swap, so any
    /// in-flight shadow-tailed writes have a chance to land before this index becomes live.
    /// Overridable (tests pass <see cref="TimeSpan.Zero"/>) - production DI leaves this at its
    /// 5-second default.</summary>
    private readonly TimeSpan _drainGracePeriod = drainGracePeriod ?? TimeSpan.FromSeconds(5);

    public async Task<RebuildSearchIndexResponse> Handle(RebuildSearchIndexCommand request, CancellationToken cancellationToken)
    {
        var newIndexName = await indexAdmin.CreateNewProductsIndexAsync(cancellationToken);
        logger.LogInformation("Rebuild: created new index {IndexName}", newIndexName);

        rebuildCoordinator.BeginShadow(newIndexName);

        long indexed = 0;
        try
        {
            await foreach (var row in snapshotReader.ReadAllAsync(cancellationToken))
            {
                await indexAdmin.BulkIndexAsync(newIndexName, row, cancellationToken);
                indexed++;
            }

            logger.LogInformation("Rebuild: backfilled {Count} documents into {IndexName}, draining before alias swap", indexed, newIndexName);

            await Task.Delay(_drainGracePeriod, cancellationToken);

            await indexAdmin.SwapAliasAsync(newIndexName, cancellationToken);
            logger.LogInformation("Rebuild: swapped search-products-active alias to {IndexName}", newIndexName);
        }
        finally
        {
            rebuildCoordinator.EndShadow();
        }

        return new RebuildSearchIndexResponse(newIndexName, indexed);
    }
}
