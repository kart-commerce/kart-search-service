using System.Text;
using System.Text.Json;
using Kart.Search.Application.Features.ConsumeReviewRatingEvent;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Kart.Search.Infrastructure.Messaging;

/// <summary>
/// SRCH-6: consumes <c>ReviewSubmitted</c>/<c>ReviewUpdated</c> off
/// <c>search.review-events.queue</c> (bound to the externally-owned <c>review.exchange</c>),
/// mirrors kart-product-service's own <c>ReviewEventsConsumerHostedService</c> almost exactly.
/// Both events are mapped to the same <see cref="ConsumeReviewRatingEventCommand"/>, keyed by
/// <c>orderId</c> (not <c>reviewId</c>) - see <c>IRatingLedgerRepository</c>'s remarks for why.
/// </summary>
public sealed class ReviewEventsConsumerHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    MessageBusManifest manifest,
    ILogger<ReviewEventsConsumerHostedService> logger) : BackgroundService
{
    private const string QueueName = "search.review-events.queue";
    private const string ReviewSubmittedRoutingKey = "review.review.submitted";
    private const string ReviewUpdatedRoutingKey = "review.review.updated";
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

                logger.LogInformation("Review events consumer listening on {Queue}", QueueName);

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
                logger.LogWarning(exception, "Review events consumer lost its RabbitMQ connection - reconnecting in {Delay}", ReconnectDelay);
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

            var routingKey = RetryLadderDispatcher.GetEffectiveRoutingKey(delivery);

            ConsumeReviewRatingEventCommand command = routingKey switch
            {
                ReviewSubmittedRoutingKey => FromSubmitted(json),
                ReviewUpdatedRoutingKey => FromUpdated(json),
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

    private static ConsumeReviewRatingEventCommand FromSubmitted(string json)
    {
        var payload = JsonSerializer.Deserialize<ReviewSubmittedPayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("ReviewSubmitted payload deserialized to null.");
        return new ConsumeReviewRatingEventCommand(payload.Sku, payload.OrderId, payload.Rating);
    }

    private static ConsumeReviewRatingEventCommand FromUpdated(string json)
    {
        var payload = JsonSerializer.Deserialize<ReviewUpdatedPayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("ReviewUpdated payload deserialized to null.");
        return new ConsumeReviewRatingEventCommand(payload.Sku, payload.OrderId, payload.NewRating);
    }
}
