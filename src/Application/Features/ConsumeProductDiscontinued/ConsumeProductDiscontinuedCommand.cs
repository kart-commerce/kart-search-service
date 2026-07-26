using MediatR;

namespace Kart.Search.Application.Features.ConsumeProductDiscontinued;

/// <summary>SRCH-4: soft-remove (edge-cases.md "Discontinued Product Still Returned by Search") -
/// same guarded write path as SRCH-2/SRCH-3, no second removal code path.</summary>
public sealed record ConsumeProductDiscontinuedCommand(string Sku, DateTimeOffset DiscontinuedAt) : IRequest;
