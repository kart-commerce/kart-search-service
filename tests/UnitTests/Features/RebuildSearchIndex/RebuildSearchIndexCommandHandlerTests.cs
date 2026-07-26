using FluentAssertions;
using Kart.Search.Application.Common.Interfaces;
using Kart.Search.Application.Common.Models;
using Kart.Search.Application.Features.RebuildSearchIndex;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Search.UnitTests.Features.RebuildSearchIndex;

public sealed class RebuildSearchIndexCommandHandlerTests
{
    private readonly Mock<ISearchIndexAdmin> _indexAdmin = new();
    private readonly Mock<ICatalogSnapshotReader> _snapshotReader = new();
    private readonly Mock<IRebuildCoordinator> _rebuildCoordinator = new();

    private RebuildSearchIndexCommandHandler CreateHandler() =>
        new(_indexAdmin.Object, _snapshotReader.Object, _rebuildCoordinator.Object, NullLogger<RebuildSearchIndexCommandHandler>.Instance, TimeSpan.Zero);

    private static async IAsyncEnumerable<ProductSnapshotRow> Rows(params ProductSnapshotRow[] rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Handle_BeginsAndEndsShadowAroundTheBackfillAndSwapsAliasAfterward()
    {
        _indexAdmin.Setup(a => a.CreateNewProductsIndexAsync(It.IsAny<CancellationToken>())).ReturnsAsync("search-products-000002");
        _snapshotReader.Setup(r => r.ReadAllAsync(It.IsAny<CancellationToken>())).Returns(Rows(
            new ProductSnapshotRow("SKU-1", "Widget", null, "cat-1", null, 10, "USD", "Active", null, null, new Dictionary<string, object?>())));

        var response = await CreateHandler().Handle(new RebuildSearchIndexCommand(), CancellationToken.None);

        response.NewIndexName.Should().Be("search-products-000002");
        response.DocumentsIndexed.Should().Be(1);

        _rebuildCoordinator.Verify(c => c.BeginShadow("search-products-000002"), Times.Once);
        _indexAdmin.Verify(a => a.BulkIndexAsync("search-products-000002", It.IsAny<ProductSnapshotRow>(), It.IsAny<CancellationToken>()), Times.Once);
        _indexAdmin.Verify(a => a.SwapAliasAsync("search-products-000002", It.IsAny<CancellationToken>()), Times.Once);
        _rebuildCoordinator.Verify(c => c.EndShadow(), Times.Once);
    }

    [Fact]
    public async Task Handle_BackfillThrows_StillEndsShadowAndDoesNotSwapAlias()
    {
        _indexAdmin.Setup(a => a.CreateNewProductsIndexAsync(It.IsAny<CancellationToken>())).ReturnsAsync("search-products-000002");
        _snapshotReader.Setup(r => r.ReadAllAsync(It.IsAny<CancellationToken>())).Returns(Rows(
            new ProductSnapshotRow("SKU-1", "Widget", null, "cat-1", null, 10, "USD", "Active", null, null, new Dictionary<string, object?>())));
        _indexAdmin
            .Setup(a => a.BulkIndexAsync("search-products-000002", It.IsAny<ProductSnapshotRow>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = async () => await CreateHandler().Handle(new RebuildSearchIndexCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _rebuildCoordinator.Verify(c => c.EndShadow(), Times.Once);
        _indexAdmin.Verify(a => a.SwapAliasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
