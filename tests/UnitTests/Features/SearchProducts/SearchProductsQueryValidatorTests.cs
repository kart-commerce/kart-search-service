using FluentAssertions;
using Kart.Search.Application.Features.SearchProducts;
using Xunit;

namespace Kart.Search.UnitTests.Features.SearchProducts;

public sealed class SearchProductsQueryValidatorTests
{
    private readonly SearchProductsQueryValidator _validator = new();

    private static SearchProductsQuery Valid() => new(null, [], null, null, null, "relevance", 1, 20);

    [Fact]
    public void Validate_DefaultQuery_IsValid()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_QueryLongerThan200Chars_IsInvalid()
    {
        var query = Valid() with { Q = new string('a', 201) };
        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("relevance")]
    [InlineData("price_asc")]
    [InlineData("price_desc")]
    [InlineData("rating_desc")]
    public void Validate_AllowedSortValues_AreValid(string sort)
    {
        var query = Valid() with { Sort = sort };
        _validator.Validate(query).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UnknownSortValue_IsInvalid()
    {
        var query = Valid() with { Sort = "popularity" };
        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_SizeAboveOneHundred_IsInvalid()
    {
        var query = Valid() with { Size = 101 };
        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_PriceMinGreaterThanPriceMax_IsInvalid()
    {
        var query = Valid() with { PriceMin = 100, PriceMax = 50 };
        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RatingMinOutOfRange_IsInvalid()
    {
        var query = Valid() with { RatingMin = 6 };
        _validator.Validate(query).IsValid.Should().BeFalse();
    }
}
