using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace Kart.Search.Infrastructure.Search;

/// <summary>Index-lifecycle half of the blue-green rebuild (SRCH-9, design-decisions.md).</summary>
public sealed class OpenSearchIndexAdmin(
    OpenSearchHttpClient client,
    IOptions<OpenSearchOptions> options,
    ICategoryLookupRepository categoryLookupRepository) : ISearchIndexAdmin
{
    public async Task<string> CreateNewProductsIndexAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var nextSuffix = await FindNextIndexSuffixAsync(opts.ProductsIndexPrefix, cancellationToken);
        var indexName = $"{opts.ProductsIndexPrefix}{nextSuffix:D6}";

        await client.PutAsync($"/{indexName}", OpenSearchIndexMappings.Products(opts.ProductsShards, opts.ProductsReplicas), cancellationToken);
        return indexName;
    }

    public async Task BulkIndexAsync(string indexName, ProductSnapshotRow row, CancellationToken cancellationToken)
    {
        var categoryName = await categoryLookupRepository.GetCategoryNameAsync(row.CategoryId, cancellationToken);
        var now = OpenSearchDates.ToIso(DateTimeOffset.UtcNow);

        var document = new
        {
            sku = row.Sku,
            name = row.Name,
            description = row.Description,
            category = new { categoryId = row.CategoryId, categoryName },
            brand = row.Brand,
            price = new { amount = row.PriceAmount, currency = row.PriceCurrency },
            availability = row.Status,
            size = row.Size,
            color = row.Color,
            sponsored = row.ExtendedAttributes.TryGetValue("sponsored", out var sponsored) && sponsored is bool flag && flag,
            extendedAttributesRaw = row.ExtendedAttributes,
            rating = new { avg = 0.0, count = 0 },
            lastCatalogEventAt = now,
            lastUpdatedAt = now,
        };

        await client.IndexAsync(indexName, row.Sku, document, routing: null, cancellationToken);
    }

    public async Task SwapAliasAsync(string indexName, CancellationToken cancellationToken)
    {
        var alias = options.Value.ProductsAlias;
        var actions = new List<object>();

        var current = await client.GetRawAsync($"/{alias}/_alias", cancellationToken);
        if (current is not null)
        {
            foreach (var indexProperty in current.RootElement.EnumerateObject())
            {
                actions.Add(new { remove = new { index = indexProperty.Name, alias } });
            }
        }

        actions.Add(new { add = new { index = indexName, alias } });

        await client.PostAsync("/_aliases", new { actions }, cancellationToken);
    }

    private async Task<int> FindNextIndexSuffixAsync(string prefix, CancellationToken cancellationToken)
    {
        var catResponse = await client.GetRawAsync($"/_cat/indices/{prefix}*?format=json", cancellationToken);
        if (catResponse is null)
        {
            return 1;
        }

        var maxSuffix = 0;
        foreach (var entry in catResponse.RootElement.EnumerateArray())
        {
            var name = entry.GetProperty("index").GetString() ?? string.Empty;
            if (name.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(name.AsSpan(prefix.Length), out var suffix)
                && suffix > maxSuffix)
            {
                maxSuffix = suffix;
            }
        }

        return maxSuffix + 1;
    }
}
