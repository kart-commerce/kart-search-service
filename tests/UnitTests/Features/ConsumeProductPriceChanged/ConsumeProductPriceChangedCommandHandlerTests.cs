using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Features.ConsumeProductPriceChanged;
using Kart.Search.Domain.SearchDocuments;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Search.UnitTests.Features.ConsumeProductPriceChanged;

public sealed class ConsumeProductPriceChangedCommandHandlerTests
{
    private readonly Mock<ISearchProjectionRepository> _projectionRepository = new();

    private ConsumeProductPriceChangedCommandHandler CreateHandler() =>
        new(_projectionRepository.Object, NullLogger<ConsumeProductPriceChangedCommandHandler>.Instance);

    [Fact]
    public async Task Handle_DelegatesToProjectionRepositoryWithNewPriceAndOccurredAt()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var newPrice = new Money(25m, "USD");
        _projectionRepository
            .Setup(r => r.ApplyPriceChangeAsync("SKU-1", newPrice, occurredAt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateHandler().Handle(new ConsumeProductPriceChangedCommand("SKU-1", newPrice, occurredAt), CancellationToken.None);

        _projectionRepository.Verify(r => r.ApplyPriceChangeAsync("SKU-1", newPrice, occurredAt, It.IsAny<CancellationToken>()), Times.Once);
    }
}
