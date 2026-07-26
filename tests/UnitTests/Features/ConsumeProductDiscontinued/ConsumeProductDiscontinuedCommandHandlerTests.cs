using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Features.ConsumeProductDiscontinued;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Search.UnitTests.Features.ConsumeProductDiscontinued;

public sealed class ConsumeProductDiscontinuedCommandHandlerTests
{
    private readonly Mock<ISearchProjectionRepository> _projectionRepository = new();

    private ConsumeProductDiscontinuedCommandHandler CreateHandler() =>
        new(_projectionRepository.Object, NullLogger<ConsumeProductDiscontinuedCommandHandler>.Instance);

    [Fact]
    public async Task Handle_DelegatesToProjectionRepositorysGuardedSoftRemove()
    {
        var discontinuedAt = DateTimeOffset.UtcNow;
        _projectionRepository.Setup(r => r.MarkDiscontinuedAsync("SKU-1", discontinuedAt, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await CreateHandler().Handle(new ConsumeProductDiscontinuedCommand("SKU-1", discontinuedAt), CancellationToken.None);

        _projectionRepository.Verify(r => r.MarkDiscontinuedAsync("SKU-1", discontinuedAt, It.IsAny<CancellationToken>()), Times.Once);
    }
}
