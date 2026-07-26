using FluentValidation;

namespace Kart.Search.Application.Features.SearchProducts;

/// <summary>Basic input-shape validation (api-contract.yaml) - the 5-combined-filter cap and the
/// pagination-window cap are business-rule checks, not shape validation, so they're enforced in
/// the handler via <c>FacetFilterLimitExceededException</c>/<c>PaginationWindowExceededException</c>
/// instead (api-standards.md: domain/business errors use exceptions/Result at the global-handler
/// boundary, not FluentValidation).</summary>
public sealed class SearchProductsQueryValidator : AbstractValidator<SearchProductsQuery>
{
    private static readonly string[] AllowedSorts = ["relevance", "price_asc", "price_desc", "rating_desc"];

    public SearchProductsQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(200);
        RuleFor(x => x.Sort).Must(sort => AllowedSorts.Contains(sort)).WithMessage("sort must be one of: relevance, price_asc, price_desc, rating_desc.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Size).InclusiveBetween(1, 100);
        RuleFor(x => x.PriceMin).GreaterThanOrEqualTo(0).When(x => x.PriceMin.HasValue);
        RuleFor(x => x.PriceMax).GreaterThanOrEqualTo(0).When(x => x.PriceMax.HasValue);
        RuleFor(x => x).Must(x => !x.PriceMin.HasValue || !x.PriceMax.HasValue || x.PriceMin <= x.PriceMax)
            .WithMessage("priceMin must not exceed priceMax.");
        RuleFor(x => x.RatingMin).InclusiveBetween(0, 5).When(x => x.RatingMin.HasValue);
    }
}
