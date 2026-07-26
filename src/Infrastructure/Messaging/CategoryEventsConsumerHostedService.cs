using System.Text;
using System.Text.Json;
using Kart.Search.Application.Features.ConsumeCategoryUpdated;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Kart.Search.Infrastructure.Messaging;

/// <summary>SRCH-5: consumes <c>CategoryUpdated</c> off <c>search.category-events.queue</c>
/// (bound to the externally-owned <c>category.exchange</c>).</summary>
public sealed class CategoryEventsConsumerHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    MessageBusManifest manifest,
    ILogger<CategoryEventsConsumerHostedService> logger) : BackgroundService
{
    private const string QueueName = "search.category-events.queue";
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

                logger.LogInformation("Category events consumer listening on {Queue}", QueueName);

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
                logger.LogWarning(exception, "Category events consumer lost its RabbitMQ connection - reconnecting in {Delay}", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task OnMessageAsync(IModel channel, QueueDefinition queue, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(delivery.Body.ToArray());
            var payload = JsonSerializer.Deserialize<CategoryUpdatedPayload>(json, JsonOptions)
                ?? throw new InvalidOperationException("CategoryUpdated payload deserialized to null.");

            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var command = new ConsumeCategoryUpdatedCommand(payload.CategoryId, payload.Name, payload.OccurredAt);
            await sender.Send(command, cancellationToken);

            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            RetryLadderDispatcher.HandleFailure(channel, delivery, queue, logger, exception);
        }
    }
}
