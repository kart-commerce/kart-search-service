using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.RabbitMq;
using Xunit;

namespace Kart.Search.IntegrationTests.Fixtures;

/// <summary>
/// Real RabbitMQ + real OpenSearch, shared across every test in the collection (container startup
/// is expensive - this mirrors kart-product-service's own <c>FullPipelineContainerFixture</c>
/// pattern of one shared fixture per test collection rather than per test).
/// </summary>
public sealed class FullPipelineContainerFixture : IAsyncLifetime
{
    // RabbitMQ's built-in guest user is restricted to true loopback connections by default
    // (loopback_users) - a connection through Testcontainers' mapped host port does not satisfy
    // that check and is rejected with ACCESS_REFUSED. A non-guest user is not subject to it.
    public const string RabbitMqUserName = "kart_search_test";
    public const string RabbitMqPassword = "changeme";

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .WithUsername(RabbitMqUserName)
        .WithPassword(RabbitMqPassword)
        .Build();

    public IContainer OpenSearch { get; } = new ContainerBuilder()
        .WithImage("opensearchproject/opensearch:2.11.0")
        .WithEnvironment("discovery.type", "single-node")
        .WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
        .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
        .WithPortBinding(9200, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9200).ForPath("/_cluster/health").ForStatusCode(HttpStatusCode.OK)))
        .Build();

    public string OpenSearchUri => $"http://{OpenSearch.Hostname}:{OpenSearch.GetMappedPublicPort(9200)}";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(RabbitMq.StartAsync(), OpenSearch.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(RabbitMq.StopAsync(), OpenSearch.StopAsync());
    }
}
