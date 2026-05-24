using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PaymentSystem.Infrastructure.CQRS.Behaviors;
using PaymentSystem.Infrastructure.Services;
using System.Reflection;

namespace PaymentSystem.Infrastructure;

public static class InfrastructureConfiguration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<AuthService>();
        services.AddScoped<SubscriptionPlanService>();
        services.AddScoped<StripeSubscriptionService>();
        return services;
    }
}
