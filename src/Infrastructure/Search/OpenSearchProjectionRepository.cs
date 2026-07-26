using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;
using Kart.Search.Domain.SearchDocuments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kart.Search.Infrastructure.Search;

/// <summary>
/// Implements the guarded write path database-design.md specifies literally
/// (<c>POST search-products-active/_update/{sku}</c> with a script asserting the
/// <c>lastCatalogEventAt</c> guard before applying <c>$set</c>-equivalent field writes) - the
/// guard is enforced atomically inside the Painless script itself, never by reading the document
/// into <see cref="SearchDocument"/> and writing it back (which would race concurrent consumers of
/// different event types touching the same document).
///
/// Every write also mirrors (best-effort, never blocking or failing the primary write) into
/// <see cref="IRebuildCoordinator.ShadowIndexName"/> when a blue-green rebuild is in progress
/// (SRCH-9) - this is the "tail live events during replay" mechanism.
/// </summary>
public sealed class OpenSearchProjectionRepository(
    OpenSearchHttpClient client,
    IOptions<OpenSearchOptions> options,
    IRebuildCoordinator rebuildCoordinator,
    ILogger<OpenSearchProjectionRepository> logger) : ISearchProjectionRepository
{
    private const string GuardPreamble = """
        Instant incoming = Instant.parse(params.occurredAt);
        Instant stored = ctx._source.lastCatalogEventAt != null ? Instant.parse(ctx._source.lastCatalogEventAt) : Instant.EPOCH;
        if (incoming.isAfter(stored)) {
        """;

    public async Task CreateAsync(SearchDocument document, CancellationToken cancellationToken)
    {
        var body = ToIndexDocument(document);
        var alias = options.Value.ProductsAlias;

        await client.IndexAsync(alias, document.Sku, body, routing: null, cancellationToken);
        await MirrorToShadowAsync(shadow => client.IndexAsync(shadow, document.Sku, body, routing: null, cancellationToken));

        logger.LogInformation("Indexed SearchDocument {Sku}", document.Sku);
    }

    public async Task<bool> ApplyPriceChangeAsync(string sku, Money newPrice, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var script = GuardPreamble + """
              ctx._source.price = params.price;
              ctx._source.lastCatalogEventAt = params.occurredAt;
              ctx._source.lastUpdatedAt = params.now;
            } else {
              ctx.op = 'noop';
            }
            """;

        var body = new
        {
            script = new
            {
                lang = "painless",
                source = script,
                @params = new
                {
                    occurredAt = OpenSearchDates.ToIso(occurredAt),
                    now = OpenSearchDates.ToIso(DateTimeOffset.UtcNow),
                    price = new { amount = newPrice.Amount, currency = newPrice.Currency },
                },
            },
        };

        var applied = await client.ScriptedUpdateAsync(options.Value.ProductsAlias, sku, body, routing: null, cancellationToken);
        await MirrorToShadowAsync(shadow => client.ScriptedUpdateAsync(shadow, sku, body, routing: null, cancellationToken));
        return applied;
    }

    public async Task<bool> ApplyCatalogUpdateAsync(string sku, CatalogUpdateFields fields, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var script = GuardPreamble + """
              if (params.name != null) { ctx._source.name = params.name; }
              if (params.description != null) { ctx._source.description = params.description; }
              if (params.categoryId != null) {
                ctx._source.category.categoryId = params.categoryId;
                ctx._source.category.categoryName = params.categoryName;
              }
              if (params.brand != null) { ctx._source.brand = params.brand; }
              if (params.size != null) { ctx._source.size = params.size; }
              if (params.color != null) { ctx._source.color = params.color; }
              if (params.sponsored != null) { ctx._source.sponsored = params.sponsored; }
              if (params.extendedAttributesRaw != null) { ctx._source.extendedAttributesRaw = params.extendedAttributesRaw; }
              ctx._source.lastCatalogEventAt = params.occurredAt;
              ctx._source.lastUpdatedAt = params.now;
            } else {
              ctx.op = 'noop';
            }
            """;

        var body = new
        {
            script = new
            {
                lang = "painless",
                source = script,
                @params = new Dictionary<string, object?>
                {
                    ["occurredAt"] = OpenSearchDates.ToIso(occurredAt),
                    ["now"] = OpenSearchDates.ToIso(DateTimeOffset.UtcNow),
                    ["name"] = fields.Name,
                    ["description"] = fields.Description,
                    ["categoryId"] = fields.CategoryId,
                    ["categoryName"] = fields.CategoryName,
                    ["brand"] = fields.Brand,
                    ["size"] = fields.Attributes?.Size,
                    ["color"] = fields.Attributes?.Color,
                    ["sponsored"] = fields.Attributes is null ? null : (object)fields.Attributes.Sponsored,
                    ["extendedAttributesRaw"] = fields.Attributes?.ExtendedAttributesRaw,
                },
            },
        };

        var applied = await client.ScriptedUpdateAsync(options.Value.ProductsAlias, sku, body, routing: null, cancellationToken);
        await MirrorToShadowAsync(shadow => client.ScriptedUpdateAsync(shadow, sku, body, routing: null, cancellationToken));
        return applied;
    }

    public async Task<bool> MarkDiscontinuedAsync(string sku, DateTimeOffset discontinuedAt, CancellationToken cancellationToken)
    {
        var script = GuardPreamble + """
              ctx._source.availability = 'Discontinued';
              ctx._source.lastCatalogEventAt = params.occurredAt;
              ctx._source.lastUpdatedAt = params.now;
            } else {
              ctx.op = 'noop';
            }
            """;

        var body = new
        {
            script = new
            {
                lang = "painless",
                source = script,
                @params = new
                {
                    occurredAt = OpenSearchDates.ToIso(discontinuedAt),
                    now = OpenSearchDates.ToIso(DateTimeOffset.UtcNow),
                },
            },
        };

        var applied = await client.ScriptedUpdateAsync(options.Value.ProductsAlias, sku, body, routing: null, cancellationToken);
        await MirrorToShadowAsync(shadow => client.ScriptedUpdateAsync(shadow, sku, body, routing: null, cancellationToken));
        return applied;
    }

    public async Task ApplyRatingAsync(string sku, RatingSignal rating, CancellationToken cancellationToken)
    {
        const string script = """
            ctx._source.rating = params.rating;
            ctx._source.lastUpdatedAt = params.now;
            """;

        var body = new
        {
            script = new
            {
                lang = "painless",
                source = script,
                @params = new
                {
                    rating = new { avg = rating.Avg, count = rating.Count },
                    now = OpenSearchDates.ToIso(DateTimeOffset.UtcNow),
                },
            },
        };

        await client.ScriptedUpdateAsync(options.Value.ProductsAlias, sku, body, routing: null, cancellationToken);
        await MirrorToShadowAsync(shadow => client.ScriptedUpdateAsync(shadow, sku, body, routing: null, cancellationToken));
    }

    private async Task MirrorToShadowAsync(Func<string, Task> write)
    {
        var shadow = rebuildCoordinator.ShadowIndexName;
        if (shadow is null)
        {
            return;
        }

        try
        {
            await write(shadow);
        }
        catch (Exception exception)
        {
            // Best-effort only - the live alias write above already succeeded; a shadow-mirroring
            // failure must never surface as a failure of the real write path.
            logger.LogWarning(exception, "Failed to mirror write into rebuild shadow index {ShadowIndex}", shadow);
        }
    }

    private static object ToIndexDocument(SearchDocument document) => new
    {
        sku = document.Sku,
        name = document.Name,
        description = document.Description,
        category = new { categoryId = document.Category.CategoryId, categoryName = document.Category.CategoryName },
        brand = document.Brand,
        price = new { amount = document.Price.Amount, currency = document.Price.Currency },
        availability = document.Availability.ToString(),
        size = document.Attributes.Size,
        color = document.Attributes.Color,
        sponsored = document.Attributes.Sponsored,
        extendedAttributesRaw = document.Attributes.ExtendedAttributesRaw,
        rating = new { avg = document.Rating.Avg, count = document.Rating.Count },
        lastCatalogEventAt = OpenSearchDates.ToIso(document.LastCatalogEventAt),
        lastUpdatedAt = OpenSearchDates.ToIso(document.LastUpdatedAt),
    };
}
