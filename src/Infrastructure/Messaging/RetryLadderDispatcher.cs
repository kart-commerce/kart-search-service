using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Kart.Search.Infrastructure.Messaging;

/// <summary>
/// Shared retry/DLQ mechanics for every consumer hosted service in this project (there are three -
/// one per publisher this service consumes from - all needing the identical manifest-driven
/// TTL-ladder-then-DLQ behavior) - extracted once here instead of duplicating it per consumer.
/// </summary>
public static class RetryLadderDispatcher
{
    private const string RetryCountHeader = "x-search-retry-count";

    /// <summary>
    /// A TTL-ladder retry bounces a message through the default exchange with the routing key set
    /// to the retry-tier queue's own name (see <see cref="HandleFailure"/>) - so by the time
    /// RabbitMQ redelivers it to the main queue, <see cref="BasicDeliverEventArgs.RoutingKey"/> no
    /// longer reflects the routing key it originally arrived with. Consumers that need to know
    /// the original routing key (to resolve which event type this is) must read this header
    /// instead of <c>RoutingKey</c> directly - see <see cref="GetEffectiveRoutingKey"/>.
    /// </summary>
    private const string OriginalRoutingKeyHeader = "x-search-original-routing-key";

    /// <summary>The routing key this message actually arrived with on its very first delivery,
    /// regardless of how many retry-ladder bounces it has since been through.</summary>
    public static string GetEffectiveRoutingKey(BasicDeliverEventArgs delivery)
    {
        if (delivery.BasicProperties.Headers is not null
            && delivery.BasicProperties.Headers.TryGetValue(OriginalRoutingKeyHeader, out var value)
            && value is byte[] bytes)
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        return delivery.RoutingKey;
    }

    /// <summary>Acks the original delivery (it is being handled one way or another) and either
    /// republishes into the next retry tier, or - once every tier is exhausted - routes directly
    /// to the queue's configured dead-letter target.</summary>
    public static void HandleFailure(IModel channel, BasicDeliverEventArgs delivery, QueueDefinition queue, ILogger logger, Exception exception)
    {
        channel.BasicAck(delivery.DeliveryTag, multiple: false);

        var retryCount = GetRetryCount(delivery.BasicProperties);
        var tiers = queue.RetryLadder.Tiers;

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = delivery.BasicProperties.ContentType;
        properties.Headers = new Dictionary<string, object>
        {
            [RetryCountHeader] = retryCount + 1,
            [OriginalRoutingKeyHeader] = GetEffectiveRoutingKey(delivery),
        };

        if (retryCount < tiers.Count)
        {
            var tier = tiers[retryCount];
            channel.BasicPublish(exchange: string.Empty, routingKey: tier.Name, basicProperties: properties, body: delivery.Body.ToArray());
            logger.LogWarning(exception, "Retrying message from {Queue} via tier {Tier} (attempt {Attempt})", queue.Name, tier.Name, retryCount + 1);
        }
        else
        {
            channel.BasicPublish(exchange: queue.DeadLetter.Exchange, routingKey: queue.DeadLetter.RoutingKey, basicProperties: properties, body: delivery.Body.ToArray());
            logger.LogError(exception, "Exhausted retry ladder for {Queue} - routed to dead-letter queue via {Exchange}/{RoutingKey}", queue.Name, queue.DeadLetter.Exchange, queue.DeadLetter.RoutingKey);
        }
    }

    private static int GetRetryCount(IBasicProperties properties)
    {
        if (properties.Headers is not null && properties.Headers.TryGetValue(RetryCountHeader, out var value))
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                byte[] bytes => int.Parse(System.Text.Encoding.UTF8.GetString(bytes)),
                _ => 0,
            };
        }

        return 0;
    }
}
