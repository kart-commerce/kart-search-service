using FluentAssertions;
using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;
using Kart.Search.Application.Features.ConsumeProductUpdated;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Search.UnitTests.Features.ConsumeProductUpdated;

public sealed class ConsumeProductUpdatedCommandHandlerTests
{
    private readonly Mock<ISearchProjectionRepository> _projectionRepository = new();
    private readonly Mock<ICategoryLookupRepository> _categoryLookupRepository = new();

    private ConsumeProductUpdatedCommandHandler CreateHandler() =>
        new(_projectionRepository.Object, _categoryLookupRepository.Object, NullLogger<ConsumeProductUpdatedCommandHandler>.Instance);

    [Fact]
    public async Task Handle_CategoryIdProvided_ReResolvesCategoryNameFromLookup()
    {
        _categoryLookupRepository.Setup(r => r.GetCategoryNameAsync("cat-2", It.IsAny<CancellationToken>())).ReturnsAsync("New Category");
        _projectionRepository
            .Setup(r => r.ApplyCatalogUpdateAsync("SKU-1", It.IsAny<CatalogUpdateFields>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new ConsumeProductUpdatedCommand("SKU-1", null, null, "cat-2", null, null, DateTimeOffset.UtcNow);
        await CreateHandler().Handle(command, CancellationToken.None);

        _projectionRepository.Verify(r => r.ApplyCatalogUpdateAsync(
            "SKU-1",
            It.Is<CatalogUpdateFields>(f => f.CategoryId == "cat-2" && f.CategoryName == "New Category"),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CategoryIdNotProvided_NeverConsultsCategoryLookup()
    {
        _projectionRepository
            .Setup(r => r.ApplyCatalogUpdateAsync(It.IsAny<string>(), It.IsAny<CatalogUpdateFields>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new ConsumeProductUpdatedCommand("SKU-1", "New Name", null, null, null, null, DateTimeOffset.UtcNow);
        await CreateHandler().Handle(command, CancellationToken.None);

        _categoryLookupRepository.Verify(r => r.GetCategoryNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
