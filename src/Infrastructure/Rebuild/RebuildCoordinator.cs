using Kart.Search.Application.Common.Interfaces;

namespace Kart.Search.Infrastructure.Rebuild;

/// <summary>Singleton in-memory implementation of <see cref="IRebuildCoordinator"/> - see its
/// remarks for what this coordinates. A plain volatile field is sufficient: at most one rebuild
/// runs at a time (an operational, infrequent action), and every reader only ever needs the
/// latest value, never a consistent snapshot across multiple reads.</summary>
public sealed class RebuildCoordinator : IRebuildCoordinator
{
    private volatile string? _shadowIndexName;

    public string? ShadowIndexName => _shadowIndexName;

    public void BeginShadow(string indexName) => _shadowIndexName = indexName;

    public void EndShadow() => _shadowIndexName = null;
}
