using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PaymentSystem.Core.CQRS.Subscriptions;
using PaymentSystem.Core.DTOs;
using PaymentSystem.Infrastructure;
using PaymentSystem.Infrastructure.BackgroundServices;
using PaymentSystem.Infrastructure.Data;
using PaymentSystem.Web.Middleware;
using Stripe;
using Stripe.Checkout;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

if (builder.Configuration.GetValue<bool>("Supabase:UseRestStore")
    && !builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHttpClient<IAppDataStore, SupabaseDataStore>();
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("PaymentSystemDb"));
    builder.Services.AddScoped<IAppDataStore, EfAppDataStore>();
}

builder.Services.AddInfrastructureServices();
builder.Services.AddHostedService<SubscriptionExpiryMonitor>();

builder.Services.AddValidatorsFromAssembly(Assembly.Load("PaymentSystem.Core"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SUPER_SECRET_RELIABLE_KEY_12345!")),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                return uri.Host is "localhost" or "127.0.0.1" || origin.EndsWith(".vercel.app");
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/register", async (PaymentSystem.Core.CQRS.Authentication.RegisterUserCommand command, IMediator mediator) =>
{
    var result = await mediator.Send(command);
    return result.IsSuccess ? Results.Ok(result) : Results.Conflict(result);
})
   .WithTags("Authentication (CQRS)");

app.MapPost("/api/auth/login", async (PaymentSystem.Core.CQRS.Authentication.LoginUserQuery query, IMediator mediator) =>
{
    var result = await mediator.Send(query);
    return result.IsAuthenticated ? Results.Ok(result) : Results.Unauthorized();
})
   .WithTags("Authentication (CQRS)");

app.MapGet("/api/subscriptions/plans", async (IMediator mediator) =>
    Results.Ok(await mediator.Send(new GetSubscriptionPlansQuery())))
   .WithTags("Subscriptions (CQRS)");

app.MapGet("/api/users/me", async (ClaimsPrincipal principal, IMediator mediator) =>
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(await mediator.Send(new GetUserProfileQuery(userId)));
})
   .RequireAuthorization()
   .WithTags("Users (CQRS)");

app.MapGet("/api/subscriptions/current", async (ClaimsPrincipal principal, IMediator mediator) =>
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var profile = await mediator.Send(new GetUserProfileQuery(userId));
    return Results.Ok(profile.ActiveSubscription);
})
   .RequireAuthorization()
   .WithTags("Subscriptions (CQRS)");

app.MapPost("/api/subscriptions", async (
    CreateSubscriptionRequest request,
    ClaimsPrincipal principal,
    HttpRequest httpRequest,
    IMediator mediator) =>
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var origin = httpRequest.Headers.Origin.FirstOrDefault()
        ?? $"{httpRequest.Scheme}://{httpRequest.Host}";

    var result = await mediator.Send(new CreateSubscriptionCommand(
        userId,
        request.Tier,
        request.Duration,
        $"{origin}/dashboard?checkout=success",
        $"{origin}/dashboard?checkout=cancel"));

    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
})
   .RequireAuthorization()
   .WithTags("Subscriptions (CQRS)");

app.MapPost("/api/stripe/webhook", async (
    HttpRequest request,
    IConfiguration configuration,
    IMediator mediator) =>
{
    var webhookSecret = configuration["Stripe:WebhookSecret"];
    if (string.IsNullOrWhiteSpace(webhookSecret))
    {
        return Results.BadRequest(new { message = "Stripe webhook secret is not configured." });
    }

    var signature = request.Headers["Stripe-Signature"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(signature))
    {
        return Results.BadRequest(new { message = "Missing Stripe-Signature header." });
    }

    string payload;
    using (var reader = new StreamReader(request.Body))
    {
        payload = await reader.ReadToEndAsync();
    }

    Event stripeEvent;
    try
    {
        stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);
    }
    catch (StripeException)
    {
        return Results.BadRequest(new { message = "Invalid Stripe webhook signature." });
    }

    if (stripeEvent.Type == "checkout.session.completed"
        && stripeEvent.Data.Object is Session session
        && session.Metadata.TryGetValue("user_id", out var rawUserId)
        && Guid.TryParse(rawUserId, out var userId))
    {
        await mediator.Send(new ActivateSubscriptionFromStripeCommand(
            userId,
            session.SubscriptionId));
    }

    return Results.Ok(new { received = true });
})
   .WithTags("Stripe");

app.Run();

static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
{
    var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(rawUserId, out userId);
}

public partial class Program { }
