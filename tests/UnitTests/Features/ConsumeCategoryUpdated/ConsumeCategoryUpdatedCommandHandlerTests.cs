using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Features.ConsumeCategoryUpdated;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Search.UnitTests.Features.ConsumeCategoryUpdated;

public sealed class ConsumeCategoryUpdatedCommandHandlerTests
{
    private readonly Mock<ICategoryLookupRepository> _categoryLookupRepository = new();

    private ConsumeCategoryUpdatedCommandHandler CreateHandler() =>
        new(_categoryLookupRepository.Object, NullLogger<ConsumeCategoryUpdatedCommandHandler>.Instance);

    [Fact]
    public async Task Handle_DelegatesToCategoryLookupRepositorysGuardedUpsert()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        _categoryLookupRepository.Setup(r => r.UpsertAsync("cat-1", "Widgets", occurredAt, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await CreateHandler().Handle(new ConsumeCategoryUpdatedCommand("cat-1", "Widgets", occurredAt), CancellationToken.None);

        _categoryLookupRepository.Verify(r => r.UpsertAsync("cat-1", "Widgets", occurredAt, It.IsAny<CancellationToken>()), Times.Once);
    }
}
