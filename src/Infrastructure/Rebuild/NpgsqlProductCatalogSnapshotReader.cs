using System.Runtime.CompilerServices;
using System.Text.Json;
using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kart.Search.Infrastructure.Rebuild;

/// <summary>
/// Rebuild-only (SRCH-9): a scheduled, keyset-paginated bulk read against a read-replica of
/// kart-product-service's <c>variants</c>/<c>product_groups</c> tables (database-design.md's
/// Rebuild Backfill Mechanism) - raw ADO.NET via Npgsql, not EF Core, since this service never
/// owns or migrates that schema, only reads it off the live request path.
/// </summary>
public sealed class NpgsqlProductCatalogSnapshotReader(IOptions<ProductCatalogSnapshotOptions> options) : ICatalogSnapshotReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string Sql = """
        SELECT v.sku, pg.name, pg.description, pg.category_id, pg.brand,
               v.price_amount, v.price_currency, v.status, v.size, v.color, v.extended_attributes,
               pg.image_url
        FROM variants v
        JOIN product_groups pg ON pg.id = v.product_group_id
        WHERE v.sku > @lastSku
        ORDER BY v.sku
        LIMIT @batchSize
        """;

    public async IAsyncEnumerable<ProductSnapshotRow> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var opts = options.Value;
        await using var connection = new NpgsqlConnection(opts.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var lastSku = string.Empty;
        while (true)
        {
            var rows = new List<ProductSnapshotRow>();

            await using (var command = new NpgsqlCommand(Sql, connection))
            {
                command.Parameters.AddWithValue("lastSku", lastSku);
                command.Parameters.AddWithValue("batchSize", opts.BatchSize);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var extendedAttributesJson = reader.IsDBNull(10) ? "{}" : reader.GetString(10);
                    var extendedAttributes = JsonSerializer.Deserialize<Dictionary<string, object?>>(extendedAttributesJson, JsonOptions)
                        ?? new Dictionary<string, object?>();

                    rows.Add(new ProductSnapshotRow(
                        Sku: reader.GetString(0),
                        Name: reader.GetString(1),
                        Description: reader.IsDBNull(2) ? null : reader.GetString(2),
                        CategoryId: reader.GetString(3),
                        Brand: reader.IsDBNull(4) ? null : reader.GetString(4),
                        PriceAmount: (double)reader.GetDecimal(5),
                        PriceCurrency: reader.GetString(6),
                        Status: reader.GetString(7),
                        Size: reader.IsDBNull(8) ? null : reader.GetString(8),
                        Color: reader.IsDBNull(9) ? null : reader.GetString(9),
                        ExtendedAttributes: extendedAttributes,
                        ImageUrl: reader.IsDBNull(11) ? null : reader.GetString(11)));
                }
            }

            if (rows.Count == 0)
            {
                yield break;
            }

            foreach (var row in rows)
            {
                yield return row;
            }

            lastSku = rows[^1].Sku;

            if (rows.Count < opts.BatchSize)
            {
                yield break;
            }
        }
    }
}
