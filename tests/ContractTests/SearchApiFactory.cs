using Kart.Search.ContractTests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kart.Search.ContractTests;

/// <summary>
/// Boots the real Api + Application pipeline (Program.cs unchanged), swapping the OpenSearch-backed
/// query repository for an in-memory fake and removing every hosted service (RabbitMQ topology/
/// consumers, OpenSearch index bootstrap) - none of which can reach a real broker/cluster here.
/// Asserts HTTP wire-shape only (status codes, JSON field names), never touching a real OpenSearch.
/// Mirrors kart-product-service's own <c>ProductApiFactory</c>.
/// </summary>
public sealed class SearchApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IHostedService));

            services.RemoveAll(typeof(Kart.Search.Application.Common.Interfaces.ISearchQueryRepository));
            services.AddSingleton<Kart.Search.Application.Common.Interfaces.ISearchQueryRepository, InMemorySearchQueryRepository>();
        });
    }
}
