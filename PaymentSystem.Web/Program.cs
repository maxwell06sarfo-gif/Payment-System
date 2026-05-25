using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PaymentSystem.Core.CQRS.Authentication;
using PaymentSystem.Core.CQRS.Subscriptions;
using PaymentSystem.Core.DTOs;
using PaymentSystem.Infrastructure;
using PaymentSystem.Infrastructure.Data;
using PaymentSystem.Web.Middleware;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// DATA STORE REGISTRATION
// Conditionally wire Supabase REST or local in-memory EF Core.
// Supabase is only used when the URL and ServiceRoleKey are
// both configured and the environment is not "Testing".
// ============================================================
var useSupabase = builder.Configuration.GetValue<bool>("Supabase:UseRestStore")
    && !builder.Environment.IsEnvironment("Testing")
    && !string.IsNullOrWhiteSpace(builder.Configuration["Supabase:ServiceRoleKey"]);

if (useSupabase)
{
    builder.Services.AddHttpClient<IAppDataStore, SupabaseDataStore>();
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("PaymentSystemDb"));
    builder.Services.AddScoped<IAppDataStore, EfAppDataStore>();
}

// ============================================================
// INFRASTRUCTURE SERVICES
// Registers: MediatR, FluentValidation, CQRS pipeline,
// application services, and the expiry background service.
// ============================================================
builder.Services.AddInfrastructureServices();

// ============================================================
// JSON + ENUM SERIALISATION
// ============================================================
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ============================================================
// JWT AUTHENTICATION + AUTHORISATION
// ============================================================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]
                    ?? throw new InvalidOperationException("Jwt:Key is not configured."))),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PaymentSystem",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PaymentSystemUsers"
        };
    });

builder.Services.AddAuthorization();

// ============================================================
// RATE LIMITING — Essential for 1 Million+ Concurrent Users
// ============================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("Strict", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(10);
        opt.PermitLimit = 100; // Allow 100 requests per 10 seconds per user/IP
        opt.QueueLimit = 20;
    });
});

// ============================================================
// CORS — accept local development and Vercel preview URLs
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                return uri.Host is "localhost" or "127.0.0.1"
                    || origin.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ============================================================
// GLOBAL EXCEPTION HANDLER + SWAGGER
// ============================================================
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PaymentSystem API",
        Version = "v1",
        Description = "Subscription billing API — Promotion, Gold, Diamond plans via Stripe."
    });

    // Allow JWT testing from Swagger UI
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ============================================================
// HTTP PIPELINE
// ============================================================
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "PaymentSystem API v1");
    options.RoutePrefix = "swagger";
});
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ============================================================
// AUTH ENDPOINTS
// ============================================================
app.MapPost("/api/auth/register", async (
    RegisterUserCommand command,
    IMediator mediator) =>
{
    var result = await mediator.Send(command);
    return result.IsSuccess
        ? Results.Ok(result)
        : Results.Conflict(result);
})
.RequireRateLimiting("Strict")
.WithTags("Authentication")
.WithSummary("Register a new user account");

app.MapPost("/api/auth/login", async (
    LoginUserQuery query,
    IMediator mediator) =>
{
    var result = await mediator.Send(query);
    return result.IsAuthenticated
        ? Results.Ok(result)
        : Results.Unauthorized();
})
.RequireRateLimiting("Strict")
.WithTags("Authentication")
.WithSummary("Log in and receive a JWT token");

// ============================================================
// SUBSCRIPTION PLAN ENDPOINTS
// ============================================================
app.MapGet("/api/subscriptions/plans", async (IMediator mediator) =>
    Results.Ok(await mediator.Send(new GetSubscriptionPlansQuery())))
.WithTags("Subscriptions")
.WithSummary("Get all available subscription plans with pricing");

// ============================================================
// USER PROFILE ENDPOINT
// ============================================================
app.MapGet("/api/users/me", async (
    ClaimsPrincipal principal,
    IMediator mediator) =>
{
    if (!TryGetUserId(principal, out var userId))
        return Results.Unauthorized();

    return Results.Ok(await mediator.Send(new GetUserProfileQuery(userId)));
})
.RequireAuthorization()
.WithTags("Users")
.WithSummary("Get authenticated user profile and active subscription");

app.MapGet("/api/subscriptions/current", async (
    ClaimsPrincipal principal,
    IMediator mediator) =>
{
    if (!TryGetUserId(principal, out var userId))
        return Results.Unauthorized();

    var profile = await mediator.Send(new GetUserProfileQuery(userId));
    return Results.Ok(profile.ActiveSubscription);
})
.RequireAuthorization()
.WithTags("Subscriptions")
.WithSummary("Get the current user's active subscription");

// ============================================================
// CREATE SUBSCRIPTION ENDPOINT
// ============================================================
app.MapPost("/api/subscriptions", async (
    CreateSubscriptionRequest request,
    ClaimsPrincipal principal,
    HttpRequest httpRequest,
    IMediator mediator) =>
{
    if (!TryGetUserId(principal, out var userId))
        return Results.Unauthorized();

    var origin = httpRequest.Headers.Origin.FirstOrDefault()
        ?? $"{httpRequest.Scheme}://{httpRequest.Host}";

    var result = await mediator.Send(new CreateSubscriptionCommand(
        userId,
        request.Tier,
        request.Duration,
        $"{origin}/dashboard?checkout=success",
        $"{origin}/dashboard?checkout=cancel"));

    return result.IsSuccess
        ? Results.Ok(result)
        : Results.BadRequest(result);
})
.RequireAuthorization()
.WithTags("Subscriptions")
.WithSummary("Subscribe to Promotion, Gold, or Diamond plan");

// ============================================================
// STRIPE WEBHOOK ENDPOINT
// ============================================================
app.MapPost("/api/stripe/webhook", async (
    HttpRequest request,
    IConfiguration configuration,
    IMediator mediator) =>
{
    var webhookSecret = configuration["Stripe:WebhookSecret"];
    if (string.IsNullOrWhiteSpace(webhookSecret))
        return Results.BadRequest(new { message = "Stripe webhook secret is not configured." });

    var signature = request.Headers["Stripe-Signature"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(signature))
        return Results.BadRequest(new { message = "Missing Stripe-Signature header." });

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
        await mediator.Send(new ActivateSubscriptionFromStripeCommand(userId, session.SubscriptionId));
    }

    return Results.Ok(new { received = true });
})
.WithTags("Stripe")
.WithSummary("Stripe webhook — activates subscription on successful checkout");

app.Run();

// ============================================================
// HELPERS
// ============================================================
static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
{
    var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(rawUserId, out userId);
}

public partial class Program { }
