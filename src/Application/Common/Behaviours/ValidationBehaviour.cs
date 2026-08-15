using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Search.Application.Common.Behaviours;

/// <summary>Runs every registered <see cref="IValidator{T}"/> for the request before the handler
/// runs, throwing FluentValidation's own <see cref="ValidationException"/> on failure -
/// <c>Kart.Shared.ErrorHandling</c>'s global exception handler translates it into a 400
/// <c>validation_error</c> Problem (api-standards.md: domain/business errors use Result/exceptions
/// at the global-handler boundary, never a local try/catch).</summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehaviour<TRequest, TResponse>> logger)
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
            var requestName = typeof(TRequest).Name;

            // Checkpoint-logging taxonomy stage 4 ("<Rule>ValidationFailed", Warning, reason
            // logged before throwing) generalized here for every FluentValidation validator on
            // the platform, rather than duplicated per handler - the ValidationException itself
            // is still logged once more, generically, at the API boundary by
            // Kart.Shared.ErrorHandling's global exception handler; this line is the one that's
            // greppable by Stage and carries the actual field-level reasons.
            logger.LogWarning(
                "Stage {Stage}: {RequestName} rejected — {Errors}",
                $"{requestName}ValidationFailed",
                requestName,
                string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));

            throw new ValidationException(failures);
        }

        return await next();
    }
}
