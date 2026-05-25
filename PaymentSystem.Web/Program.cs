using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PaymentSystem.Core.CQRS.Authentication;
using PaymentSystem.Core.CQRS.Subscriptions;
using PaymentSystem.Core.DTOs;
using PaymentSystem.Infrastructure;
using PaymentSystem.Infrastructure.Data;
using PaymentSystem.Infrastructure.Services;
using PaymentSystem.Web.Middleware;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// DATA STORE
// Supabase REST is used when explicitly enabled, a service role key is present,
// and we are not running inside the test harness. Any other combination falls
// back to EF Core with an in-memory database, which keeps integration tests
// isolated and removes the need for a live Supabase instance during local dev.
// ---------------------------------------------------------------------------
var useSupabase = builder.Configuration["Supabase:UseRestStore"] == "true"
    && !builder.Environment.IsEnvironment("Testing")
    && !string.IsNullOrWhiteSpace(builder.Configuration["Supabase:ServiceRoleKey"])
    && !string.IsNullOrWhiteSpace(builder.Configuration["Supabase:Url"]);

if (useSupabase)
{
    builder.Services.AddHttpClient<IAppDataStore, SupabaseDataStore>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // Optimise for 1 million concurrent users by increasing connection pooling
            MaxConnectionsPerServer = 500
        });
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("PaymentSystemDb"));
    builder.Services.AddScoped<IAppDataStore, EfAppDataStore>();
}

// ---------------------------------------------------------------------------
// INFRASTRUCTURE SERVICES
// MediatR, FluentValidation, the CQRS pipeline behavior, application services,
// and the background subscription expiry monitor all register here.
// ---------------------------------------------------------------------------
builder.Services.AddInfrastructureServices();

// ---------------------------------------------------------------------------
// JSON / ENUM SERIALISATION
// Enums are written as strings in the JSON contract so API consumers get
// readable values ("Gold") instead of raw integers (2).
// ---------------------------------------------------------------------------
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ---------------------------------------------------------------------------
// FAIL-FAST VALIDATION
// ---------------------------------------------------------------------------
var criticalConfigs = new Dictionary<string, string>
{
    { "Jwt:Key", "Provide at least 32 characters via Jwt__Key." },
    { "Stripe:SecretKey", "Required via Stripe__SecretKey." },
    { "Stripe:WebhookSecret", "Required via Stripe__WebhookSecret." }
};

foreach (var config in criticalConfigs)
{
    var val = builder.Configuration[config.Key];
    if (string.IsNullOrWhiteSpace(val) || (config.Key == "Jwt:Key" && val.Length < 32))
        throw new InvalidOperationException($"CRITICAL CONFIG MISSING: {config.Key}. {config.Value}");
}

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PaymentSystem",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PaymentSystemUsers",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// RATE LIMITING
// A fixed-window limiter on auth and mutation endpoints. The queue absorbs
// short bursts without immediately returning 429s to legitimate clients.
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("Strict", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(10);
        opt.PermitLimit = 100;
        opt.QueueLimit = 20;
    });
});

// ---------------------------------------------------------------------------
// CORS
// Origins are resolved in three layers, checked in order:
//   1. App:AllowedOrigins — explicit comma-separated list from environment/config
//      (set this on Render to lock down the exact production/preview URLs)
//   2. Localhost — always allowed for local development
//   3. *.vercel.app — wildcard for Vercel preview deployments
// ---------------------------------------------------------------------------
var explicitOrigins = (builder.Configuration["App:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                // Always permit localhost for dev.
                if (uri.Host is "localhost" or "127.0.0.1")
                    return true;

                // Permit all Vercel preview deployments.
                if (origin.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
                    return true;

                // Explicit allow-list takes priority — use this for production URLs.
                if (explicitOrigins.Count > 0 && explicitOrigins.Contains(origin.TrimEnd('/')))
                    return true;
                return false; // If none of the above, deny.
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------------
// EXCEPTION HANDLER + SWAGGER
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// MIDDLEWARE PIPELINE
// CORS must run immediately after the exception handler so that preflight
// responses and error responses both carry Access-Control-Allow-Origin.
// Placing it any later means the browser rejects the response before it
// even inspects the status code.
// ---------------------------------------------------------------------------
app.UseExceptionHandler();
app.UseCors("Frontend");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PaymentSystem API v1");
        options.RoutePrefix = "swagger";
    });
}
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Health check endpoint — confirms the service is running and reports the current build version.
app.MapGet("/health", () => Results.Ok(new { status = "ok", build = "v6-build-failure-fixed" }))
   .WithTags("Health")
   .ExcludeFromDescription();

// ---------------------------------------------------------------------------
// AUTH ENDPOINTS
// ---------------------------------------------------------------------------
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
        : Results.Problem(
            statusCode: 401,
            title: "Unauthorized",
            detail: "Invalid email or password");
})
.RequireRateLimiting("Strict")
.WithTags("Authentication")
.WithSummary("Log in and receive a JWT token");

app.MapPost("/api/auth/refresh", async (
    RefreshTokenRequest request,
    IMediator mediator) =>
{
    var result = await mediator.Send(new RefreshTokenCommand(request.Token));
    return result.IsAuthenticated
        ? Results.Ok(result)
        : Results.Unauthorized();
})
.WithTags("Authentication")
.WithSummary("Exchange a refresh token for a new JWT and Refresh Token");

// ---------------------------------------------------------------------------
// SUBSCRIPTION PLAN ENDPOINTS
// ---------------------------------------------------------------------------
app.MapGet("/api/subscriptions/plans", async (IMediator mediator) =>
    Results.Ok(await mediator.Send(new GetSubscriptionPlansQuery())))
.WithTags("Subscriptions")
.WithSummary("Get all available subscription plans with pricing");

// ---------------------------------------------------------------------------
// USER PROFILE
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// CREATE SUBSCRIPTION
// ---------------------------------------------------------------------------
app.MapPost("/api/subscriptions", async (
    CreateSubscriptionRequest request,
    ClaimsPrincipal principal,
    HttpRequest httpRequest,
    IMediator mediator) =>
{
    if (!TryGetUserId(principal, out var userId))
        return Results.Unauthorized();

    var origin = ResolveRedirectBase(httpRequest, app.Configuration);

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
.RequireRateLimiting("Strict")
.WithTags("Subscriptions")
.WithSummary("Subscribe to Promotion, Gold, or Diamond plan");

app.MapPost("/api/subscriptions/portal", async (
    ClaimsPrincipal principal,
    HttpRequest httpRequest,
    IAppDataStore dataStore,
    StripeSubscriptionService stripeService) =>
{
    if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
    var user = await dataStore.GetUserByEmailAsync(principal.FindFirstValue(ClaimTypes.Email)!, CancellationToken.None);
    
    if (user == null || string.IsNullOrEmpty(user.StripeCustomerId)) return Results.BadRequest("No billing history found.");

    var returnUrl = ResolveRedirectBase(httpRequest, app.Configuration) + "/dashboard";
    var url = await stripeService.CreateCustomerPortalSessionAsync(user.StripeCustomerId, returnUrl);
    return Results.Ok(new { url });
})
.RequireAuthorization()
.WithTags("Subscriptions")
.WithSummary("Generate a Stripe Billing Portal link for self-service management");

// ---------------------------------------------------------------------------
// STRIPE WEBHOOK
// Stripe signs every webhook delivery with a HMAC signature. We verify it
// before trusting any payload content — an unsigned or mis-signed request
// is rejected outright rather than processed partially.
// ---------------------------------------------------------------------------
app.MapPost("/api/stripe/webhook", async (
    HttpRequest request,
    IConfiguration configuration,
    IAppDataStore dataStore,
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

    // Check for idempotency — avoid processing the same event twice
    if (await dataStore.HasWebhookBeenProcessedAsync(stripeEvent.Id, CancellationToken.None))
    {
        return Results.Ok(new { received = true, duplicated = true });
    }

    if (stripeEvent.Type == "checkout.session.completed"
        && stripeEvent.Data.Object is Session session
        && session.Metadata.TryGetValue("user_id", out var rawUserId)
        && Guid.TryParse(rawUserId, out var userId))
    {
        await mediator.Send(new ActivateSubscriptionFromStripeCommand(userId, session.SubscriptionId));
    }

    // Mark event as processed
    await dataStore.MarkWebhookAsProcessedAsync(stripeEvent.Id, CancellationToken.None);

    return Results.Ok(new { received = true });
})
.WithTags("Stripe")
.WithSummary("Stripe webhook — activates subscription on successful checkout");

app.Run();

// ---------------------------------------------------------------------------
// HELPERS
// ---------------------------------------------------------------------------
static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
{
    var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(rawUserId, out userId);
}

/// <summary>
/// Resolves the base URL used for Stripe checkout redirect callbacks.
///
/// An explicit App:FrontendUrl in configuration is always preferred. Falling back
/// to the request Origin header is a last resort for local dev only — accepting an
/// arbitrary caller-supplied origin for redirect construction would be an open
/// redirect, so the fallback is restricted to known-safe hosts.
/// </summary>
static string ResolveRedirectBase(HttpRequest request, IConfiguration configuration)
{
    var configured = configuration["App:FrontendUrl"];
    if (!string.IsNullOrWhiteSpace(configured))
        return configured.TrimEnd('/');

    var origin = request.Headers.Origin.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        return $"{request.Scheme}://{request.Host}";

    var isAllowed = uri.Host is "localhost" or "127.0.0.1"
        || origin.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase);

    return isAllowed ? origin.TrimEnd('/') : $"{request.Scheme}://{request.Host}";
}

public partial class Program { }
