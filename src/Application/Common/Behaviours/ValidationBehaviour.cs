using FluentValidation;
using MediatR;

namespace Kart.Search.Application.Common.Behaviours;

/// <summary>Runs every registered <see cref="IValidator{T}"/> for the request before the handler
/// runs, throwing FluentValidation's own <see cref="ValidationException"/> on failure -
/// <c>Kart.Shared.ErrorHandling</c>'s global exception handler translates it into a 400
/// <c>validation_error</c> Problem (api-standards.md: domain/business errors use Result/exceptions
/// at the global-handler boundary, never a local try/catch).</summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
