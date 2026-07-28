using System.Text;
using System.Text.Json;
using Kart.Search.Application.Features.ConsumeProductCreated;
using Kart.Search.Application.Features.ConsumeProductDiscontinued;
using Kart.Search.Application.Features.ConsumeProductPriceChanged;
using Kart.Search.Application.Features.ConsumeProductUpdated;
using Kart.Search.Domain.SearchDocuments;
using Kart.Shared.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Kart.Search.Infrastructure.Messaging;

/// <summary>
/// SRCH-1..4: consumes <c>ProductCreated</c>/<c>ProductPriceChanged</c>/<c>ProductUpdated</c>/
/// <c>ProductDiscontinued</c> off <c>search.product-events.queue</c> (bound to the
/// externally-owned <c>product.exchange</c>), dispatching to the matching MediatR command per
/// routing key.
/// </summary>
public sealed class ProductEventsConsumerHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    MessageBusManifest manifest,
    ILogger<ProductEventsConsumerHostedService> logger) : BackgroundService
{
    private const string QueueName = "search.product-events.queue";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory { HostName = options.Value.HostName, Port = options.Value.Port, UserName = options.Value.UserName, Password = options.Value.Password, DispatchConsumersAsync = true };
                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                RabbitMqTopologyProvisioner.Declare(channel, manifest);
                channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

                var queue = manifest.GetQueue(QueueName);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, delivery) => await OnMessageAsync(channel, queue, delivery, stoppingToken);

                channel.BasicConsume(QueueName, autoAck: false, consumer);

                logger.LogInformation("Product events consumer listening on {Queue}", QueueName);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Product events consumer lost its RabbitMQ connection - reconnecting in {Delay}", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task OnMessageAsync(IModel channel, QueueDefinition queue, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(delivery.Body.ToArray());
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // Not delivery.RoutingKey directly - a retry-ladder bounce overwrites it with the
            // retry-tier queue name by the time RabbitMQ redelivers to this queue.
            var routingKey = RetryLadderDispatcher.GetEffectiveRoutingKey(delivery);

            IRequest command = routingKey switch
            {
                "product.product.created" => ToCreatedCommand(json),
                "product.price.changed" => ToPriceChangedCommand(json),
                "product.product.updated" => ToUpdatedCommand(json),
                "product.product.discontinued" => ToDiscontinuedCommand(json),
                _ => throw new InvalidOperationException($"Unrecognized routing key '{routingKey}' on {QueueName}."),
            };

            await sender.Send(command, cancellationToken);

            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            RetryLadderDispatcher.HandleFailure(channel, delivery, queue, logger, exception);
        }
    }

    private static ConsumeProductCreatedCommand ToCreatedCommand(string json)
    {
        var payload = Deserialize<ProductCreatedPayload>(json);
        return new ConsumeProductCreatedCommand(
            payload.Sku,
            payload.Name,
            payload.Description,
            payload.CategoryId,
            payload.Brand,
            new Money(payload.Price.Amount, payload.Price.Currency),
            FacetableAttributes.FromEventPayload(payload.Attributes.Size, payload.Attributes.Color, payload.Attributes.ExtendedAttributes),
            payload.OccurredAt);
    }

    private static ConsumeProductPriceChangedCommand ToPriceChangedCommand(string json)
    {
        var payload = Deserialize<ProductPriceChangedPayload>(json);
        return new ConsumeProductPriceChangedCommand(payload.Sku, new Money(payload.NewPrice.Amount, payload.NewPrice.Currency), payload.OccurredAt);
    }

    private static ConsumeProductUpdatedCommand ToUpdatedCommand(string json)
    {
        var payload = Deserialize<ProductUpdatedPayload>(json);
        var attributes = payload.Attributes is null
            ? null
            : FacetableAttributes.FromEventPayload(payload.Attributes.Size, payload.Attributes.Color, payload.Attributes.ExtendedAttributes);

        return new ConsumeProductUpdatedCommand(payload.Sku, payload.Name, payload.Description, payload.CategoryId, payload.Brand, attributes, payload.OccurredAt);
    }

    private static ConsumeProductDiscontinuedCommand ToDiscontinuedCommand(string json)
    {
        var payload = Deserialize<ProductDiscontinuedPayload>(json);
        return new ConsumeProductDiscontinuedCommand(payload.Sku, payload.DiscontinuedAt);
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException($"{typeof(T).Name} payload deserialized to null.");
}
