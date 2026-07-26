using FluentAssertions;
using Kart.Search.Domain.SearchDocuments;
using Xunit;

namespace Kart.Search.UnitTests.Domain.SearchDocuments;

public sealed class SearchDocumentTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SearchDocument CreateDocument(DateTimeOffset occurredAt) =>
        SearchDocument.Create(
            "SKU-1",
            "Widget",
            "A widget",
            "cat-1",
            "Widgets",
            "Acme",
            new Money(10m, "USD"),
            FacetableAttributes.Empty,
            occurredAt,
            occurredAt);

    [Fact]
    public void ApplyPriceChange_NewerOccurredAt_Applies()
    {
        var document = CreateDocument(BaseTime);

        var applied = document.ApplyPriceChange(new Money(20m, "USD"), BaseTime.AddSeconds(1), BaseTime.AddSeconds(1));

        applied.Should().BeTrue();
        document.Price.Amount.Should().Be(20m);
        document.LastCatalogEventAt.Should().Be(BaseTime.AddSeconds(1));
    }

    [Fact]
    public void ApplyPriceChange_OlderOrEqualOccurredAt_IsRejectedAsStaleRedelivery()
    {
        var document = CreateDocument(BaseTime);

        var applied = document.ApplyPriceChange(new Money(20m, "USD"), BaseTime, BaseTime);

        applied.Should().BeFalse();
        document.Price.Amount.Should().Be(10m);
    }

    [Fact]
    public void ApplyCatalogUpdate_NullFields_LeavesThemUnchanged()
    {
        var document = CreateDocument(BaseTime);

        var applied = document.ApplyCatalogUpdate(
            name: "Renamed Widget",
            description: null,
            categoryId: null,
            categoryName: null,
            brand: null,
            attributes: null,
            occurredAt: BaseTime.AddSeconds(1),
            now: BaseTime.AddSeconds(1));

        applied.Should().BeTrue();
        document.Name.Should().Be("Renamed Widget");
        document.Description.Should().Be("A widget");
        document.Category.CategoryId.Should().Be("cat-1");
    }

    [Fact]
    public void MarkDiscontinued_NewerDiscontinuedAt_SoftRemovesWithoutHardDelete()
    {
        var document = CreateDocument(BaseTime);

        var applied = document.MarkDiscontinued(BaseTime.AddSeconds(1), BaseTime.AddSeconds(1));

        applied.Should().BeTrue();
        document.Availability.Should().Be(Availability.Discontinued);
        document.Sku.Should().Be("SKU-1");
    }

    [Fact]
    public void MarkDiscontinued_StaleRedelivery_IsRejected()
    {
        var document = CreateDocument(BaseTime);
        document.MarkDiscontinued(BaseTime.AddSeconds(5), BaseTime.AddSeconds(5));

        var applied = document.MarkDiscontinued(BaseTime.AddSeconds(1), BaseTime.AddSeconds(1));

        applied.Should().BeFalse();
        document.Availability.Should().Be(Availability.Discontinued);
    }

    [Fact]
    public void ApplyRating_IsNeverGuardedByLastCatalogEventAt()
    {
        var document = CreateDocument(BaseTime);
        var beforeGuard = document.LastCatalogEventAt;

        document.ApplyRating(new RatingSignal(4.5, 10), BaseTime.AddDays(-1));

        document.Rating.Avg.Should().Be(4.5);
        document.Rating.Count.Should().Be(10);
        document.LastCatalogEventAt.Should().Be(beforeGuard);
    }
}
