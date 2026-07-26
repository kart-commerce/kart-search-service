# Contracts

`api-contract.yaml` and `event-contract.md` are synced copies of the approved
contracts owned by `kart-platform/docs/services/kart-search-service/` (the
source of truth), vendored here so `tests/ContractTests` can validate this
service's actual HTTP responses against them in this repo's own CI, without a
cross-repo checkout. Update them only by re-copying the upstream files after
a new contract revision is approved there - never edit them directly in this
repo.

`message-bus-manifest.json` is this service's own RabbitMQ topology, declared
idempotently at startup by `RabbitMqTopologyStartupHostedService`
(`src/Infrastructure/Messaging`), which walks this file via
`RabbitMqTopologyProvisioner` - nothing in this topology is hardcoded in C#.

**Note on shape:** this file uses the mature nested manifest schema
(`exchanges[]`/`externalExchanges[]`/`publishedEvents[]`/
`queues[].deadLetter`+`.retryLadder`/`deadLetterQueues[]`) that
`kart-identity-service`, `kart-inventory-service`, `kart-category-service`,
and `kart-product-service` all already use. The copy currently checked in at
`kart-platform/docs/services/kart-search-service/message-bus-manifest.json`
still uses an earlier, flatter shape (`exchange`/`dlx`/`queues`/`dlqs`/`retry`
as siblings) that predates that convention settling - this file reconciles
Search's manifest to the now-standard shape, the same reconciliation
`kart-product-service/contracts/message-bus-manifest.json` already documents
for itself. The upstream doc should be updated to match on its own next
revision; that is out of this repo's scope to do unilaterally.

Search publishes no events (pure consumer/query context, `event-contract.md`)
- it owns no publish exchange, only its own dead-letter exchange
(`search.dlx`). Three queues, one per publisher this service consumes from:

- `search.product-events.queue` - consumes `ProductCreated`/
  `ProductPriceChanged`/`ProductUpdated`/`ProductDiscontinued` from the
  externally-owned `product.exchange` (SRCH-1..4), 3x retry ladder.
- `search.category-events.queue` - consumes `CategoryUpdated` from the
  externally-owned `category.exchange` (SRCH-5), 3x retry ladder.
- `search.review-events.queue` - consumes `ReviewSubmitted`/`ReviewUpdated`
  from the externally-owned `review.exchange` (SRCH-6), 2x retry ladder
  (matching Review's own lower publish-side tier for both events).

Update `message-bus-manifest.json`/`api-contract.yaml`/`event-contract.md`
only by re-copying the upstream files after a revision is approved there.
