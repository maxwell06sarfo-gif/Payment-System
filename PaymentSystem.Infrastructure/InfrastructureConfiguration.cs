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
        // Register MediatR handlers from Infrastructure layer
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // Register FluentValidation validators from Core layer (proper type-safe reference)
        services.AddValidatorsFromAssembly(typeof(RegisterUserCommand).Assembly);

        // Register the MediatR validation pipeline behavior
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Register application services
        services.AddScoped<AuthService>();
        services.AddScoped<SubscriptionPlanService>();
        services.AddScoped<StripeSubscriptionService>();
        services.AddHttpClient<IEmailService, ResendEmailService>();

        // Register the background subscription expiry monitor
        services.AddHostedService<SubscriptionExpiryMonitor>();

        return services;
    }
}
