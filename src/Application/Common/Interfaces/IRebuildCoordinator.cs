namespace Kart.Search.Application.Common.Interfaces;

/// <summary>
/// In-memory singleton state implementing the "tail live events during replay" half of the
/// blue-green rebuild (SRCH-9, edge-cases.md "Index Rebuild During High Write Volume"). While a
/// rebuild is in progress, <see cref="ShadowIndexName"/> names the new, not-yet-aliased index
/// every <c>Consume*CommandHandler</c> should ALSO (best-effort, non-blocking) apply its write to,
/// in addition to the live <c>search-products-active</c> alias target - this is what lets the new
/// index catch up on events that arrive during the Postgres snapshot backfill, without needing a
/// full event-replay mechanism RabbitMQ doesn't provide (BRD §14).
/// </summary>
public interface IRebuildCoordinator
{
    string? ShadowIndexName { get; }

    void BeginShadow(string indexName);

    void EndShadow();
}
