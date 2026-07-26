namespace Kart.Search.Infrastructure.Messaging;

/// <summary>
/// Strongly-typed mirror of <c>contracts/message-bus-manifest.json</c> - this service's single
/// source of truth for its entire RabbitMQ topology (kart-identity-service's proven pattern,
/// ported verbatim from kart-product-service). Nothing in this topology is hardcoded in C# -
/// <see cref="RabbitMqTopologyProvisioner"/> scans this model and declares every exchange/queue/
/// binding/DLQ/retry-tier at startup.
/// </summary>
public sealed record MessageBusManifest(
    string Service,
    IReadOnlyList<ExchangeDefinition> Exchanges,
    IReadOnlyList<ExchangeDefinition> ExternalExchanges,
    IReadOnlyList<PublishedEventDefinition> PublishedEvents,
    IReadOnlyList<QueueDefinition> Queues,
    IReadOnlyList<DeadLetterQueueDefinition> DeadLetterQueues)
{
    public string ExchangeFor(string eventType) =>
        PublishedEvents.FirstOrDefault(e => e.EventType == eventType)?.Exchange
            ?? throw new InvalidOperationException($"No publishedEvents entry for event type '{eventType}' in the message-bus manifest.");

    public string RoutingKeyFor(string eventType) =>
        PublishedEvents.FirstOrDefault(e => e.EventType == eventType)?.RoutingKey
            ?? throw new InvalidOperationException($"No publishedEvents entry for event type '{eventType}' in the message-bus manifest.");

    public QueueDefinition GetQueue(string name) =>
        Queues.FirstOrDefault(q => q.Name == name)
            ?? throw new InvalidOperationException($"No queue named '{name}' in the message-bus manifest.");
}

public sealed record ExchangeDefinition(string Name, string Type, bool Durable);

public sealed record PublishedEventDefinition(string EventType, string Exchange, string RoutingKey);

public sealed record QueueDefinition(
    string Name,
    bool Durable,
    IReadOnlyList<QueueBindingDefinition> Bindings,
    DeadLetterDefinition DeadLetter,
    RetryLadderDefinition RetryLadder);

public sealed record QueueBindingDefinition(string Exchange, string RoutingKey);

public sealed record DeadLetterDefinition(string Exchange, string RoutingKey);

public sealed record RetryLadderDefinition(string RequeueTo, IReadOnlyList<RetryTierDefinition> Tiers);

public sealed record RetryTierDefinition(string Name, int TtlMs);

public sealed record DeadLetterQueueDefinition(string Name, string Exchange, string RoutingKey);
