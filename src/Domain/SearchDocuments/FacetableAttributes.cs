namespace Kart.Search.Domain.SearchDocuments;

/// <summary>
/// Mirrors kart-product-service's <c>ProductAttributes</c> value object unchanged in shape
/// (ddd-model.md). <see cref="Sponsored"/> is promoted out of <see cref="ExtendedAttributesRaw"/>
/// at write time (database-design.md's "Sponsored Flag: Promoted to a First-Class Field") since a
/// <c>flattened</c>-typed OpenSearch field cannot be referenced from a <c>function_score</c> scoring
/// clause - every other key from the incoming event's schemaless attributes bag stays in
/// <see cref="ExtendedAttributesRaw"/> untouched.
/// </summary>
public sealed record FacetableAttributes(
    string? Size,
    string? Color,
    bool Sponsored,
    IReadOnlyDictionary<string, object?> ExtendedAttributesRaw)
{
    public static readonly FacetableAttributes Empty = new(null, null, false, new Dictionary<string, object?>());

    /// <summary>Extracts <c>attributes.extendedAttributes.sponsored</c> (default <c>false</c>) from
    /// an incoming event's raw attributes bag, promoting it to a first-class field while leaving
    /// every other key in the untouched catch-all.</summary>
    public static FacetableAttributes FromEventPayload(string? size, string? color, IReadOnlyDictionary<string, object?>? extendedAttributes)
    {
        var raw = extendedAttributes ?? new Dictionary<string, object?>();
        var sponsored = raw.TryGetValue("sponsored", out var value) && value is bool flag && flag;
        return new FacetableAttributes(size, color, sponsored, raw);
    }
}
