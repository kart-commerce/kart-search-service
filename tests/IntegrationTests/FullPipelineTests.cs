using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Kart.Search.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Xunit;

namespace Kart.Search.IntegrationTests;

/// <summary>
/// Full-pipeline tests against real RabbitMQ + real OpenSearch (no fakes) - publishes a catalog
/// event directly onto the externally-owned exchanges this service consumes from, exactly as
/// kart-product-service/kart-category-service/kart-review-service would, then polls
/// <c>GET /v1/search</c> until the projected document becomes queryable (requirement-spec's
/// bounded eventual-consistency window).
/// </summary>
[Collection(nameof(FullPipelineCollection))]
public sealed class FullPipelineTests : IAsyncLifetime
{
    private readonly FullPipelineContainerFixture _containers;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FullPipelineTests(FullPipelineContainerFixture containers)
    {
        _containers = containers;
    }

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = _containers.RabbitMq.Hostname,
                ["RabbitMq:Port"] = _containers.RabbitMq.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:UserName"] = FullPipelineContainerFixture.RabbitMqUserName,
                ["RabbitMq:Password"] = FullPipelineContainerFixture.RabbitMqPassword,
                ["OpenSearch:Uri"] = _containers.OpenSearchUri,
            }));
        });

        _client = _factory.CreateClient();

        // Force the host (and its hosted services - topology declare, index bootstrap, consumers)
        // to actually start.
        _ = _factory.Services;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProductCreated_BecomesQueryableWithinTheFreshnessWindow()
    {
        var sku = $"SKU-{Guid.NewGuid():N}";
        await PublishAsync("product.exchange", "product.product.created", new
        {
            sku,
            name = "Integration Test Widget",
            description = "A widget for integration testing",
            categoryId = "cat-integration",
            brand = "Acme",
            price = new { amount = 42.50m, currency = "USD" },
            status = "Active",
            attributes = new { size = (string?)null, color = (string?)null, extendedAttributes = new Dictionary<string, object?>() },
            occurredAt = DateTimeOffset.UtcNow,
        });

        var found = await PollForResultAsync(sku, TimeSpan.FromSeconds(20));

        found.Should().NotBeNull();
        found!.Value.GetProperty("sku").GetString().Should().Be(sku);
        found.Value.GetProperty("name").GetString().Should().Be("Integration Test Widget");
    }

    [Fact]
    public async Task OutOfOrderProductPriceChanged_OlderRedeliveryIsRejectedByTheGuard()
    {
        var sku = $"SKU-{Guid.NewGuid():N}";
        var createdAt = DateTimeOffset.UtcNow;

        await PublishAsync("product.exchange", "product.product.created", new
        {
            sku,
            name = "Guard Test Widget",
            description = (string?)null,
            categoryId = "cat-integration",
            brand = (string?)null,
            price = new { amount = 10m, currency = "USD" },
            status = "Active",
            attributes = new { size = (string?)null, color = (string?)null, extendedAttributes = new Dictionary<string, object?>() },
            occurredAt = createdAt,
        });

        await PollForResultAsync(sku, TimeSpan.FromSeconds(20));

        // Newer price applied first...
        await PublishAsync("product.exchange", "product.price.changed", new
        {
            sku,
            oldPrice = new { amount = 10m, currency = "USD" },
            newPrice = new { amount = 30m, currency = "USD" },
            occurredAt = createdAt.AddSeconds(10),
        });

        await Task.Delay(TimeSpan.FromSeconds(3));

        // ...then a stale, older redelivery arrives - must be rejected, price stays 30.
        await PublishAsync("product.exchange", "product.price.changed", new
        {
            sku,
            oldPrice = new { amount = 10m, currency = "USD" },
            newPrice = new { amount = 15m, currency = "USD" },
            occurredAt = createdAt.AddSeconds(5),
        });

        await Task.Delay(TimeSpan.FromSeconds(3));

        var result = await PollForResultAsync(sku, TimeSpan.FromSeconds(20));
        result.Should().NotBeNull();
        result!.Value.GetProperty("price").GetProperty("amount").GetDouble().Should().Be(30);
    }

    private Task PublishAsync(string exchange, string routingKey, object payload)
    {
        var factory = new ConnectionFactory
        {
            HostName = _containers.RabbitMq.Hostname,
            Port = _containers.RabbitMq.GetMappedPublicPort(5672),
            UserName = FullPipelineContainerFixture.RabbitMqUserName,
            Password = FullPipelineContainerFixture.RabbitMqPassword,
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(exchange, "topic", durable: true);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        var properties = channel.CreateBasicProperties();
        properties.ContentType = "application/json";

        channel.BasicPublish(exchange, routingKey, properties, body);
        return Task.CompletedTask;
    }

    private async Task<JsonElement?> PollForResultAsync(string sku, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var response = await _client.GetFromJsonAsync<JsonElement>($"/v1/search?q={sku}");
            var results = response.GetProperty("results");

            foreach (var item in results.EnumerateArray())
            {
                if (item.GetProperty("sku").GetString() == sku)
                {
                    return item;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return null;
    }
}

[CollectionDefinition(nameof(FullPipelineCollection))]
public sealed class FullPipelineCollection : ICollectionFixture<FullPipelineContainerFixture>;
