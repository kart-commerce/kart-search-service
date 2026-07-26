using FluentAssertions;
using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Features.ConsumeProductCreated;
using Kart.Search.Domain.SearchDocuments;
using Kart.Search.UnitTests.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Search.UnitTests.Features.ConsumeProductCreated;

public sealed class ConsumeProductCreatedCommandHandlerTests
{
    private readonly Mock<ISearchProjectionRepository> _projectionRepository = new();
    private readonly Mock<ICategoryLookupRepository> _categoryLookupRepository = new();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private ConsumeProductCreatedCommandHandler CreateHandler() =>
        new(_projectionRepository.Object, _categoryLookupRepository.Object, NullLogger<ConsumeProductCreatedCommandHandler>.Instance, new FixedTimeProvider(Now));

    [Fact]
    public async Task Handle_ResolvesCategoryNameFromLookupAndIndexesDocument()
    {
        _categoryLookupRepository.Setup(r => r.GetCategoryNameAsync("cat-1", It.IsAny<CancellationToken>())).ReturnsAsync("Widgets");

        var command = new ConsumeProductCreatedCommand("SKU-1", "Widget", "desc", "cat-1", "Acme", new Money(10m, "USD"), FacetableAttributes.Empty, Now);

        await CreateHandler().Handle(command, CancellationToken.None);

        _projectionRepository.Verify(r => r.CreateAsync(
            It.Is<SearchDocument>(d => d.Sku == "SKU-1" && d.Category.CategoryName == "Widgets"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoCategoryLookupEntryYet_StillIndexesDocumentWithNullCategoryName()
    {
        _categoryLookupRepository.Setup(r => r.GetCategoryNameAsync("cat-1", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var command = new ConsumeProductCreatedCommand("SKU-1", "Widget", "desc", "cat-1", "Acme", new Money(10m, "USD"), FacetableAttributes.Empty, Now);

        await CreateHandler().Handle(command, CancellationToken.None);

        _projectionRepository.Verify(r => r.CreateAsync(
            It.Is<SearchDocument>(d => d.Category.CategoryName == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
