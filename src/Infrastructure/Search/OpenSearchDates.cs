using System.Globalization;

namespace Kart.Search.Infrastructure.Search;

/// <summary>
/// A fixed-precision, 'Z'-suffixed ISO-8601 timestamp format used for every date written to or
/// read from OpenSearch in this service. Deliberately NOT <see cref="DateTimeOffset"/>'s default
/// round-trip ("o") format, which renders a UTC offset as <c>+00:00</c> - Painless's
/// <c>Instant.parse</c> (used by the guarded scripted-update Painless scripts to compare
/// <c>lastCatalogEventAt</c>) requires the strict ISO instant format with a literal <c>Z</c>, and
/// rejects a numeric <c>+00:00</c> offset.
/// </summary>
public static class OpenSearchDates
{
    private const string Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public static string ToIso(DateTimeOffset value) => value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture);
}
