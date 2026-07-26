# kart-search-service

Full-text product search, filters, facets, and blended ranking (BRD §17). A pure read/query-side
service - it owns no write database of its own and publishes no events (`requirement-spec.md` §1);
its entire job is projecting catalog state into a queryable OpenSearch index.

Design docs: `kart-platform/docs/services/kart-search-service/`. Tickets: SRCH-1 through SRCH-9.

## Architecture

CQRS with **no PostgreSQL/write side at all** - a standard rebuildable projection sourced from
three other services' own write sides, not the same "no write side" exception
`kart-delivery-tracking-service` claims for itself (`database-design.md`'s Architecture Note):

- **Store**: OpenSearch, three indices - `search_products` (8 shards routed by
  `category.categoryId`, backs the `SearchDocument` aggregate), `search_category_lookup` (1 shard,
  `CategoryLookup` aggregate), `search_rating_ledger` (4 shards routed by `sku`, write-side-only
  per-review bookkeeping the `rating.avg`/`count` fields are recomputed from).
- **Sync mechanism**: async RabbitMQ consumption only - three consumer queues, one per publisher
  (`search.product-events.queue` from `product.exchange`, `search.category-events.queue` from
  `category.exchange`, `search.review-events.queue` from `review.exchange`). Every catalog-origin
  write is guarded by a `lastCatalogEventAt` version/timestamp check enforced atomically inside an
  OpenSearch scripted `_update` (rejects stale-ordered redeliveries without needing strict message
  ordering). Rating-origin writes are deliberately unguarded (disjoint field set) and idempotent by
  construction (`RatingSignal` is always recomputed from the full per-review ledger, never mutated
  incrementally).
- **Ranking**: a single-pass OpenSearch `function_score` query blending normalized text relevance,
  rating, in-stock status, and a capped sponsored-placement boost (`ddd-model.md`'s
  `RankingProfile`) - see `src/Domain/SearchDocuments/RankingProfile.cs`, the single source of
  truth for the formula's constants.
- **Message bus topology**: `contracts/message-bus-manifest.json` is the single source of truth,
  declared idempotently at startup (`RabbitMqTopologyProvisioner`) - nothing is hardcoded in C#.
  Search publishes no events, so it owns no exchange of its own, only its own DLX (`search.dlx`).
  See `contracts/README.md`.
- **Blue-green rebuild** (SRCH-9): an ops-only `POST /internal/reindex` (not part of
  `api-contract.yaml`) builds a new index, tails live events into it via `IRebuildCoordinator`'s
  in-memory "shadow index" while backfilling a Postgres snapshot of `kart-product-service`'s
  `variants`/`product_groups`, then atomically swaps the `search-products-active` alias.
- **No application-level response/facet cache** - explicitly ruled out by `design-decisions.md`
  (a cached facet aggregate would reopen the exact staleness problem the Domain Invariant
  forecloses); every `/v1/search` call queries OpenSearch live.

## Layout

Clean Architecture + Vertical Slice (`docs/standards/folder-structure.md` in
[agent-reusables](https://github.com/kakon-mehedi/agent-reusables)):

```
src/
├── Api/              # controllers (GET /v1/search, POST /internal/reindex), health checks,
│                        global exception handling, observability wiring - no auth (public read)
├── Application/       # Features/<UseCaseName>/ vertical slices (MediatR) - SRCH-1..9
├── Domain/             # SearchDocument/CategoryLookup aggregates, RankingProfile, value objects
└── Infrastructure/    # OpenSearch (this service's one data store), RabbitMQ messaging, rebuild
tests/
├── UnitTests/          # domain guard logic, every handler, the ranking formula's pure math
├── IntegrationTests/   # Testcontainers: real RabbitMQ + real OpenSearch, full event->query pipeline
└── ContractTests/      # validates live responses against contracts/api-contract.yaml
contracts/              # synced copies of the approved api-contract.yaml / event-contract.md / message-bus-manifest.json
```

Reuses `kart-shared`'s `Kart.Shared.Domain` (Result/Error base types), `Kart.Shared.ErrorHandling`
(the one global exception handler + `ProblemDetails` envelope, every error response), and
`Kart.Shared.Observability` (Serilog + OpenTelemetry + Prometheus, one DI call) via
`ProjectReference` - no published NuGet feed exists yet.

## Running locally

Requires the .NET 8 SDK and Docker.

```
dotnet build
dotnet test
```

### Full stack via docker-compose

```
docker compose up -d --build
```

This starts RabbitMQ, a single-node OpenSearch (a minimal-but-real stand-in for the production
8/1/4-shard cluster), and the service itself on `http://localhost:8083`. `OpenSearchIndexBootstrapHostedService`
creates all three indices + the `search-products-active` alias idempotently at startup.

> The Docker build context is the **parent** directory (`kart-commerce/`), not this repo alone -
> see the comment at the top of `Dockerfile`. This is a known, temporary consequence of
> `kart-shared` not yet publishing to a real NuGet feed.

### Running against locally-installed dependencies instead

```
dotnet run --project src/Api
```

Point `RabbitMq__HostName` and `OpenSearch__Uri` at your own instances via environment variables or
`appsettings.Development.json` if they differ from the defaults in `src/Api/appsettings.json`.
`ProductCatalogSnapshot__ConnectionString` is only needed to run a rebuild (`POST /internal/reindex`).

### Auth

None - `GET /v1/search` is unconditionally public (no per-user ownership dimension exists over
search-result content, `ddd-model.md`'s CanRead/CanWrite/CanDelete invariant). There is no
externally-reachable write endpoint of any kind.
