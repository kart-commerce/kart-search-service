using RabbitMQ.Client;

namespace Kart.Search.Infrastructure.Messaging;

/// <summary>
/// Declares every exchange/queue/binding/DLQ/retry-tier named in a <see cref="MessageBusManifest"/>.
/// RabbitMQ's own declare/bind operations are idempotent, so this is safe to call repeatedly (on
/// every reconnect, from more than one hosted service) without side effects beyond the first call.
/// </summary>
public static class RabbitMqTopologyProvisioner
{
    public static void Declare(IModel channel, MessageBusManifest manifest)
    {
        foreach (var exchange in manifest.Exchanges.Concat(manifest.ExternalExchanges))
        {
            channel.ExchangeDeclare(exchange.Name, exchange.Type, exchange.Durable);
        }

        foreach (var dlq in manifest.DeadLetterQueues)
        {
            channel.QueueDeclare(dlq.Name, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(dlq.Name, dlq.Exchange, dlq.RoutingKey);
        }

        foreach (var queue in manifest.Queues)
        {
            var arguments = new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = queue.DeadLetter.Exchange,
                ["x-dead-letter-routing-key"] = queue.DeadLetter.RoutingKey,
            };

            channel.QueueDeclare(queue.Name, durable: queue.Durable, exclusive: false, autoDelete: false, arguments!);

            foreach (var binding in queue.Bindings)
            {
                channel.QueueBind(queue.Name, binding.Exchange, binding.RoutingKey);
            }

            foreach (var tier in queue.RetryLadder.Tiers)
            {
                // Parking-lot / TTL-ladder retry tier: a message that lands here waits ttlMs,
                // then dead-letters (via the default exchange, routing key = requeueTo) back
                // onto the main queue for another delivery attempt.
                var tierArguments = new Dictionary<string, object>
                {
                    ["x-message-ttl"] = tier.TtlMs,
                    ["x-dead-letter-exchange"] = string.Empty,
                    ["x-dead-letter-routing-key"] = queue.RetryLadder.RequeueTo,
                };

                channel.QueueDeclare(tier.Name, durable: true, exclusive: false, autoDelete: false, tierArguments!);
            }
        }
    }
}
