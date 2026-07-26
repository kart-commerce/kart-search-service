using FluentAssertions;
using Kart.Search.Domain.SearchDocuments;
using Xunit;

namespace Kart.Search.UnitTests.Domain.SearchDocuments;

/// <summary>
/// Unit-tests the pure C# mirror of the Painless <c>script_score</c> the real query path builds
/// (Infrastructure/Search/OpenSearchSearchRepository) - the formula's own math is exercised here,
/// independent of a live OpenSearch cluster (IntegrationTests separately verify the real script).
/// </summary>
public sealed class RankingProfileTests
{
    [Fact]
    public void RatingComponent_BelowThreshold_ReturnsNeutralValue()
    {
        RankingProfile.RatingComponent(ratingAvg: 5.0, ratingCount: 1).Should().Be(RankingProfile.NeutralRatingComponent);
    }

    [Fact]
    public void RatingComponent_AtOrAboveThreshold_ReturnsNormalizedAverage()
    {
        RankingProfile.RatingComponent(ratingAvg: 4.0, ratingCount: RankingProfile.RatingCountThreshold).Should().Be(0.8);
    }

    [Fact]
    public void InStockComponent_ActiveIsOne_DiscontinuedIsZero()
    {
        RankingProfile.InStockComponent(isActive: true).Should().Be(1.0);
        RankingProfile.InStockComponent(isActive: false).Should().Be(0.0);
    }

    [Fact]
    public void NormalizeTextRelevance_IsMonotonicallyIncreasingAndBoundedBelowOne()
    {
        var low = RankingProfile.NormalizeTextRelevance(1.0);
        var high = RankingProfile.NormalizeTextRelevance(100.0);

        low.Should().BeLessThan(high);
        high.Should().BeLessThan(1.0);
    }

    [Fact]
    public void ComputeScore_SponsoredAdditiveBoostIsCappedAndAdditive()
    {
        var unsponsored = RankingProfile.ComputeScore(bm25Score: 10, ratingAvg: 4, ratingCount: 10, isActive: true, sponsored: false);
        var sponsored = RankingProfile.ComputeScore(bm25Score: 10, ratingAvg: 4, ratingCount: 10, isActive: true, sponsored: true);

        (sponsored - unsponsored).Should().BeApproximately(RankingProfile.SponsoredBoost, 1e-9);
    }

    [Fact]
    public void ComputeScore_DiscontinuedNeverReachesRankingButComponentIsZeroIfItDid()
    {
        var active = RankingProfile.ComputeScore(bm25Score: 10, ratingAvg: 4, ratingCount: 10, isActive: true, sponsored: false);
        var discontinued = RankingProfile.ComputeScore(bm25Score: 10, ratingAvg: 4, ratingCount: 10, isActive: false, sponsored: false);

        (active - discontinued).Should().BeApproximately(RankingProfile.InStockWeight, 1e-9);
    }
}
