using FluentValidation;
using Kart.Search.Application.Common.Behaviours;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Search.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Registration order is pipeline order (outermost first) - Logging wraps Validation so
        // its stage-3 HandlerStarted log still fires before a stage-4 validation-failure throw
        // (checkpoint-logging-standard.md), matching kart-identity-service's reference
        // registration order.
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
