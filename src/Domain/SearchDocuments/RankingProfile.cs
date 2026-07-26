namespace Kart.Search.Domain.SearchDocuments;

/// <summary>
/// ddd-model.md's resolved scoring formula:
///
/// <code>
/// finalScore = (textRelevanceNorm x 0.50) + (ratingComponent x 0.20) + (inStockComponent x 0.15) + sponsoredBoost
/// </code>
///
/// Implemented at query time as a single-pass OpenSearch <c>function_score</c> query
/// (<c>Infrastructure/Search/OpenSearchSearchRepository</c>), per ddd-model.md's explicit
/// "implemented via function_score, not a separate re-ranking pass." A single Painless
/// <c>script_score</c> has no visibility into sibling documents' scores, so true cross-document
/// min-max normalization of the BM25 relevance score isn't available in one pass - this build's own
/// resolved engineering default substitutes a monotonic saturating normalization
/// <c>textScore / (textScore + k)</c>, mapping BM25 relevance onto <c>[0,1)</c> (an initial,
/// revisable baseline, matching every other constant in this formula).
///
/// This class is the single source of truth for the formula's constants - the Painless script
/// built in Infrastructure reads these same values as script params (never re-typed as separate
/// literals there), and <see cref="ComputeScore"/> is a pure C# mirror of exactly what that script
/// computes, kept here specifically so the formula's math is unit-testable without a live
/// OpenSearch cluster (IntegrationTests separately verify the real Painless script against a real
/// index).
/// </summary>
public static class RankingProfile
{
    public const double TextRelevanceWeight = 0.50;
    public const double RatingWeight = 0.20;
    public const double InStockWeight = 0.15;

    /// <summary>Capped, not auctioned (ddd-model.md) - no bidding/marketplace mechanism exists
    /// anywhere in the platform's scope for an auctioned model to run against.</summary>
    public const double SponsoredBoost = 0.15;

    /// <summary>Below this many reviews, <see cref="RatingSignal.Avg"/> is not yet a reliable
    /// signal - a neutral 0.5 is used instead, guarding against a single review swinging a new
    /// product's rank as hard as an item with hundreds of reviews.</summary>
    public const int RatingCountThreshold = 5;

    public const double NeutralRatingComponent = 0.5;

    /// <summary>Tunable saturation constant for the single-pass text-relevance normalization
    /// substitute described above.</summary>
    public const double TextNormalizationK = 10.0;

    public static double NormalizeTextRelevance(double bm25Score) => bm25Score / (bm25Score + TextNormalizationK);

    public static double RatingComponent(double ratingAvg, int ratingCount) =>
        ratingCount >= RatingCountThreshold ? ratingAvg / 5.0 : NeutralRatingComponent;

    /// <summary><paramref name="isActive"/> is always <c>true</c> in production - Discontinued
    /// documents are excluded from default results before ranking ever runs (architecture.md's
    /// "In-Stock Ranking Signal Source"). Kept as a real parameter (not a hardcoded 1.0) so a
    /// future richer stock-level signal can replace this term without restructuring the formula,
    /// and so the weight itself is exercised by a unit test.</summary>
    public static double InStockComponent(bool isActive) => isActive ? 1.0 : 0.0;

    public static double ComputeScore(double bm25Score, double ratingAvg, int ratingCount, bool isActive, bool sponsored)
    {
        var textNorm = NormalizeTextRelevance(bm25Score);
        var rating = RatingComponent(ratingAvg, ratingCount);
        var inStock = InStockComponent(isActive);
        var sponsoredBoost = sponsored ? SponsoredBoost : 0.0;

        return (textNorm * TextRelevanceWeight) + (rating * RatingWeight) + (inStock * InStockWeight) + sponsoredBoost;
    }
}
