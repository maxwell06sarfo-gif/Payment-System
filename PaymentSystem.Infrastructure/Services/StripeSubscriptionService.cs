using Microsoft.Extensions.Configuration;
using PaymentSystem.Core.Enums;
using Stripe;
using Stripe.Checkout;

namespace PaymentSystem.Infrastructure.Services;

public class StripeSubscriptionService
{
    private readonly IConfiguration _configuration;
    private readonly SubscriptionPlanService _planService;

    public StripeSubscriptionService(IConfiguration configuration, SubscriptionPlanService planService)
    {
        _configuration = configuration;
        _planService = planService;
        // Do NOT throw here — this service is always injected even when Stripe
        // is not configured. The plans endpoint (and other non-Stripe paths)
        // must succeed regardless. Configuration is validated lazily in the
        // methods that actually talk to Stripe.
    }

    /// <summary>
    /// Configures the Stripe SDK API key and throws a clear exception if
    /// the secret key is missing. Called only inside methods that need Stripe.
    /// </summary>
    private void EnsureStripeConfigured()
    {
        var apiKey = _configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Stripe:SecretKey is missing. Provide Stripe__SecretKey via environment variables.");

        StripeConfiguration.ApiKey = apiKey;
    }

    public async Task<string> CreateStripeCustomerAsync(string email, string fullName)
    {
        EnsureStripeConfigured();

        var options = new CustomerCreateOptions
        {
            Email = email,
            Name = fullName,
            Description = "PaymentSystem Managed Customer Account"
        };

        var service = new CustomerService();
        Customer customer = await service.CreateAsync(options);
        return customer.Id;
    }

    public bool CanUseHostedCheckout(SubscriptionTier tier, SubscriptionDuration duration)
    {
        var apiKey = _configuration["Stripe:SecretKey"];

        return !string.IsNullOrWhiteSpace(apiKey)
            && apiKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase)
            && !apiKey.Contains("mock", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> CreateCheckoutSessionAsync(
        string stripeCustomerId,
        SubscriptionTier tier,
        SubscriptionDuration duration,
        Guid userId,
        string successUrl,
        string cancelUrl)
    {
        EnsureStripeConfigured();

        var options = new SessionCreateOptions
        {
            Customer = stripeCustomerId,
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            AllowPromotionCodes = true,
            BillingAddressCollection = "auto",
            ClientReferenceId = userId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["user_id"] = userId.ToString(),
                ["tier"] = tier.ToString(),
                ["duration"] = duration.ToString()
            },
            LineItems = new List<SessionLineItemOptions>
            {
                BuildCheckoutLineItem(tier, duration)
            }
        };

        var service = new SessionService();
        Session session = await service.CreateAsync(options);
        return session.Url;
    }

    private SessionLineItemOptions BuildCheckoutLineItem(
        SubscriptionTier tier,
        SubscriptionDuration duration)
    {
        var configuredPriceId = GetConfiguredStripePriceId(tier, duration);
        if (!string.IsNullOrWhiteSpace(configuredPriceId))
        {
            return new SessionLineItemOptions
            {
                Price = configuredPriceId,
                Quantity = 1
            };
        }

        var priceInCents = decimal.ToInt64(_planService.GetPlanPrice(tier, duration) * 100m);

        return new SessionLineItemOptions
        {
            Quantity = 1,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = "usd",
                UnitAmount = priceInCents,
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = $"{tier} subscription",
                    Description = $"{duration} access to the {tier} promotion service."
                },
                Recurring = BuildRecurringOptions(duration)
            }
        };
    }

    private static SessionLineItemPriceDataRecurringOptions BuildRecurringOptions(SubscriptionDuration duration)
    {
        return duration switch
        {
            SubscriptionDuration.SixMonths => new SessionLineItemPriceDataRecurringOptions
            {
                Interval = "month",
                IntervalCount = 6
            },
            SubscriptionDuration.Yearly => new SessionLineItemPriceDataRecurringOptions
            {
                Interval = "year",
                IntervalCount = 1
            },
            _ => new SessionLineItemPriceDataRecurringOptions
            {
                Interval = "month",
                IntervalCount = 1
            }
        };
    }

    private string? GetConfiguredStripePriceId(SubscriptionTier tier, SubscriptionDuration duration)
    {
        return _configuration[$"Stripe:PriceIds:{tier}:{duration}"];
    }
}
