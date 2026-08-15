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

        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
