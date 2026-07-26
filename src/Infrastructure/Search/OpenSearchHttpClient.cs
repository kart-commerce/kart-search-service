using System.Net;
using System.Text;
using System.Text.Json;

namespace Kart.Search.Infrastructure.Search;

/// <summary>
/// Thin typed-<see cref="HttpClient"/> wrapper (registered via <c>AddHttpClient</c> for pooled,
/// reused connections - important at this service's target throughput) around OpenSearch's plain
/// REST API. This service talks to OpenSearch this way rather than through a NEST-style typed
/// client package - every query shape this service needs (<c>function_score</c>/<c>script_score</c>
/// ranking, scripted-update guards, multi-facet aggregations with graceful degradation, alias
/// swaps) is a raw JSON body it must construct precisely either way, and OpenSearch's REST API
/// itself is the stable, versioned contract.
/// </summary>
public sealed class OpenSearchHttpClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Plain GET against an arbitrary path (e.g. <c>_cat/indices/...?format=json</c>),
    /// returning <c>null</c> on 404 rather than throwing.</summary>
    public async Task<JsonDocument?> GetRawAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, path), cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task PutAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsync(path, ToJsonContent(body), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>Unconditional index/replace (used for document creation and rebuild backfill,
    /// neither of which needs a guard).</summary>
    public async Task IndexAsync(string index, string id, object document, string? routing, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsync(BuildPath($"/{Escape(index)}/_doc/{Escape(id)}", routing), ToJsonContent(document), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>Scripted <c>_update</c> call - the guard itself lives inside <paramref name="updateBody"/>'s
    /// Painless script (<c>ctx.op = 'noop'</c> when rejected). Returns <c>false</c> (never applied)
    /// both when the guard rejected the write and when the target document doesn't exist yet
    /// (404) - neither is treated as an error, per requirement-spec's "never blocks" posture.</summary>
    public async Task<bool> ScriptedUpdateAsync(string index, string id, object updateBody, string? routing, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(BuildPath($"/{Escape(index)}/_update/{Escape(id)}", routing), ToJsonContent(updateBody), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var result = document.RootElement.GetProperty("result").GetString();
        return result != "noop";
    }

    /// <summary>Same guarded scripted-update semantics as <see cref="ScriptedUpdateAsync"/>, but
    /// additionally returns the affected document's post-update <c>_source</c> (via the update
    /// API's own <c>?_source=true</c> option) - avoids a second round-trip when the caller needs
    /// to read back a field the script just wrote (the rating ledger's recomputed map).</summary>
    public async Task<JsonElement?> ScriptedUpdateAndGetSourceAsync(string index, string id, object updateBody, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync($"/{Escape(index)}/_update/{Escape(id)}?_source=true", ToJsonContent(updateBody), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("get").GetProperty("_source").Clone();
    }

    public async Task<JsonDocument> SearchAsync(string indexOrAlias, object query, TimeSpan? clientTimeout, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{Escape(indexOrAlias)}/_search") { Content = ToJsonContent(query) };

        using var cts = clientTimeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        cts?.CancelAfter(clientTimeout!.Value);

        using var response = await httpClient.SendAsync(request, cts?.Token ?? cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    public async Task PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(path, ToJsonContent(body), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<JsonDocument?> GetAsync(string index, string id, string? routing, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(BuildPath($"/{Escape(index)}/_doc/{Escape(id)}", routing), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string BuildPath(string basePath, string? routing) =>
        string.IsNullOrEmpty(routing) ? basePath : $"{basePath}?routing={Uri.EscapeDataString(routing)}";

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static StringContent ToJsonContent(object body) =>
        new(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenSearch request to {response.RequestMessage?.RequestUri} failed ({(int)response.StatusCode}): {body}");
        }
    }
}
