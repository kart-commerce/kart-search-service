using Kart.Search.Domain.SearchDocuments;
using MediatR;

namespace Kart.Search.Application.Features.ConsumeProductCreated;

/// <summary>SRCH-1: the aggregate-creation trigger for <see cref="SearchDocument"/>
/// (ddd-model.md) - dispatched by <c>ProductEventsConsumerHostedService</c> on
/// <c>product.product.created</c>.</summary>
public sealed record ConsumeProductCreatedCommand(
    string Sku,
    string Name,
    string? Description,
    string CategoryId,
    string? Brand,
    Money Price,
    FacetableAttributes Attributes,
    DateTimeOffset OccurredAt) : IRequest;
