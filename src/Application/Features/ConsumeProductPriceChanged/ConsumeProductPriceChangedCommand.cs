using Kart.Search.Domain.SearchDocuments;
using MediatR;

namespace Kart.Search.Application.Features.ConsumeProductPriceChanged;

/// <summary>SRCH-2: guarded price update, applied only if <see cref="OccurredAt"/> is strictly
/// newer than the stored <c>lastCatalogEventAt</c> (edge-cases.md "Out-of-Order Event
/// Consumption").</summary>
public sealed record ConsumeProductPriceChangedCommand(string Sku, Money NewPrice, DateTimeOffset OccurredAt) : IRequest;
