using FluentAssertions;
using Kart.Search.Domain.CategoryLookups;
using Xunit;

namespace Kart.Search.UnitTests.Domain.CategoryLookups;

public sealed class CategoryLookupTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apply_NewerOccurredAt_Applies()
    {
        var lookup = CategoryLookup.Create("cat-1", "Widgets", BaseTime);

        var applied = lookup.Apply("Gadgets", BaseTime.AddSeconds(1));

        applied.Should().BeTrue();
        lookup.CategoryName.Should().Be("Gadgets");
    }

    [Fact]
    public void Apply_StaleRedelivery_IsRejected()
    {
        var lookup = CategoryLookup.Create("cat-1", "Widgets", BaseTime);

        var applied = lookup.Apply("Gadgets", BaseTime.AddSeconds(-1));

        applied.Should().BeFalse();
        lookup.CategoryName.Should().Be("Widgets");
    }
}
