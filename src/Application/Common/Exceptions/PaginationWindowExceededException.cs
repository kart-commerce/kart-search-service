namespace Kart.Search.Application.Common.Exceptions;

/// <summary>api-contract.yaml's <c>PAGINATION_WINDOW_EXCEEDED</c> - <c>page * size</c> exceeds the
/// 10,000-row OpenSearch <c>index.max_result_window</c> cap (database-design.md).</summary>
public sealed class PaginationWindowExceededException(int page, int size)
    : Exception($"page ({page}) * size ({size}) exceeds the 10,000-row pagination window.")
{
    public int Page { get; } = page;
    public int Size { get; } = size;
}
