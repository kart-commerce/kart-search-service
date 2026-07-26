using MediatR;

namespace Kart.Search.Application.Features.ConsumeCategoryUpdated;

/// <summary>SRCH-5: maintains <c>CategoryLookup</c> only - never fans out to every
/// <c>SearchDocument</c> referencing the category (ddd-model.md's Cross-Aggregate
/// Interaction).</summary>
public sealed record ConsumeCategoryUpdatedCommand(string CategoryId, string Name, DateTimeOffset OccurredAt) : IRequest;
