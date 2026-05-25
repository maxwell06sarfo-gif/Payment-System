using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PaymentSystem.Core.CQRS.Authentication;
using PaymentSystem.Infrastructure.BackgroundServices;
using PaymentSystem.Infrastructure.CQRS.Behaviors;
using PaymentSystem.Infrastructure.Services;
using System.Reflection;

namespace PaymentSystem.Infrastructure;

public static class InfrastructureConfiguration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // MediatR scans this assembly for all IRequestHandler implementations at startup.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // Validators live in the Core assembly — reference by type to avoid a magic string assembly name.
        services.AddValidatorsFromAssembly(typeof(RegisterUserCommand).Assembly);

        // Runs every request through FluentValidation before it reaches its handler.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<AuthService>();
        services.AddScoped<SubscriptionPlanService>();
        services.AddScoped<StripeSubscriptionService>();

        // Runs on a configurable interval (default 24 h) to expire stale subscriptions
        // and stamp expiration notification timestamps on records approaching end date.
        services.AddHostedService<SubscriptionExpiryMonitor>();

        return services;
    }
}
