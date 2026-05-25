using MediatR;
using PaymentSystem.Core.Constants;
using PaymentSystem.Core.CQRS.Subscriptions;
using PaymentSystem.Core.DTOs;
using PaymentSystem.Core.Entities;
using PaymentSystem.Core.Enums;
using PaymentSystem.Infrastructure.Data;
using PaymentSystem.Infrastructure.Services;

namespace PaymentSystem.Infrastructure.CQRS.Handlers;

public class SubscriptionHandler :
    IRequestHandler<GetSubscriptionPlansQuery, IReadOnlyCollection<SubscriptionPlanResponse>>,
    IRequestHandler<GetUserProfileQuery, UserProfileResponse>,
    IRequestHandler<CreateSubscriptionCommand, CreateSubscriptionResponse>,
    IRequestHandler<ActivateSubscriptionFromStripeCommand, bool>
{
    private readonly IAppDataStore _dataStore;
    private readonly SubscriptionPlanService _planService;
    private readonly StripeSubscriptionService _stripeService;

    public SubscriptionHandler(
        IAppDataStore dataStore,
        SubscriptionPlanService planService,
        StripeSubscriptionService stripeService)
    {
        _dataStore = dataStore;
        _planService = planService;
        _stripeService = stripeService;
    }

    public Task<IReadOnlyCollection<SubscriptionPlanResponse>> Handle(
        GetSubscriptionPlansQuery request,
        CancellationToken ct)
    {
        IReadOnlyCollection<SubscriptionPlanResponse> plans = new[]
        {
            BuildPlan(SubscriptionTier.Promotion, "Promotion", "Entry-level promotion access for customers testing paid visibility.", new[]
            {
                "Promotion service access",
                "Monthly, 6-month, or yearly billing",
                "Expiration alerts in dashboard"
            }),
            BuildPlan(SubscriptionTier.Gold, "Gold", "A stronger plan for customers who need more campaign reach.", new[]
            {
                "Everything in Promotion",
                "Priority promotion placement",
                "Stripe checkout handoff"
            }),
            BuildPlan(SubscriptionTier.Diamond, "Diamond", "The top subscription for customers running premium promotion campaigns.", new[]
            {
                "Everything in Gold",
                "Premium campaign reach",
                "Renewal reminders before expiration"
            })
        };

        return Task.FromResult(plans);
    }

    public async Task<UserProfileResponse> Handle(GetUserProfileQuery request, CancellationToken ct)
    {
        var user = await _dataStore.GetUserWithSubscriptionsAsync(request.UserId, ct);

        if (user is null)
        {
            throw new KeyNotFoundException("User profile was not found.");
        }

        var activeSubscription = GetCurrentSubscription(user.Subscriptions);

        return new UserProfileResponse(
            new UserResponse(user.Id, user.Email, user.FullName, user.StripeCustomerId),
            activeSubscription is null ? null : MapSubscription(activeSubscription));
    }

    public async Task<CreateSubscriptionResponse> Handle(CreateSubscriptionCommand request, CancellationToken ct)
    {
        var validation = _planService.ValidateSubscriptionRequest(
            new CreateSubscriptionRequest(request.Tier, request.Duration));

        if (!validation.IsValid)
        {
            return new CreateSubscriptionResponse(false, validation.ErrorMessage, null, null);
        }

        var user = await _dataStore.GetUserWithSubscriptionsAsync(request.UserId, ct);

        if (user is null)
        {
            throw new KeyNotFoundException("User profile was not found.");
        }

        var price = _planService.GetPlanPrice(request.Tier, request.Duration);
        var endsAt = request.Duration switch
        {
            SubscriptionDuration.Monthly => DateTime.UtcNow.AddMonths(1),
            SubscriptionDuration.SixMonths => DateTime.UtcNow.AddMonths(6),
            SubscriptionDuration.Yearly => DateTime.UtcNow.AddYears(1),
            _ => DateTime.UtcNow.AddMonths(1)
        };

        string? checkoutUrl = null;
        var status = SubscriptionStatus.Active;

        if (_stripeService.CanUseHostedCheckout(request.Tier, request.Duration))
        {
            if (string.IsNullOrWhiteSpace(user.StripeCustomerId))
            {
                user.StripeCustomerId = await _stripeService.CreateStripeCustomerAsync(user.Email, user.FullName);
            }

            checkoutUrl = await _stripeService.CreateCheckoutSessionAsync(
                user.StripeCustomerId!,
                request.Tier,
                request.Duration,
                request.UserId,
                request.SuccessUrl,
                request.CancelUrl);

            status = SubscriptionStatus.PendingCheckout;
        }
        else if (string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            user.StripeCustomerId = BuildDemoCustomerId(user.Id);
        }

        await _dataStore.UpdateUserAsync(user, ct);

        var newSubscription = new Subscription
        {
            UserId = user.Id,
            Tier = request.Tier,
            Duration = request.Duration,
            Price = price,
            Currency = "USD",
            Status = status,
            StartsAt = DateTime.UtcNow,
            EndsAt = endsAt,
            StripeSubscriptionId = checkoutUrl is null ? "demo_local_subscription" : null
        };

        // For Stripe-hosted checkouts, replacement happens when the webhook fires — not here.
        // For local/demo activations we can promote immediately since there is no async handoff.
        if (status == SubscriptionStatus.Active)
        {
            await _dataStore.MarkActiveSubscriptionsReplacedAsync(user.Id, ct);
        }

        await _dataStore.AddSubscriptionAsync(newSubscription, ct);

        var message = checkoutUrl is null
            ? "Subscription updated in the local billing engine."
            : "Stripe checkout session created.";

        return new CreateSubscriptionResponse(
            true,
            message,
            checkoutUrl,
            MapSubscription(newSubscription));
    }

    public async Task<bool> Handle(ActivateSubscriptionFromStripeCommand request, CancellationToken ct)
    {
        await _dataStore.ActivateLatestPendingCheckoutAsync(
            request.UserId,
            request.StripeSubscriptionId,
            ct);

        return true;
    }

    private SubscriptionPlanResponse BuildPlan(
        SubscriptionTier tier,
        string name,
        string description,
        string[] features)
    {
        return new SubscriptionPlanResponse(
            tier,
            name,
            description,
            _planService.GetPlanPrice(tier, SubscriptionDuration.Monthly),
            _planService.GetPlanPrice(tier, SubscriptionDuration.SixMonths),
            _planService.GetPlanPrice(tier, SubscriptionDuration.Yearly),
            features);
    }

    private static Subscription? GetCurrentSubscription(IEnumerable<Subscription> subscriptions)
    {
        // PendingCheckout, Replaced, and Expired records must never surface as the user's
        // current plan. This tripped us up after cancellations — the most-recent record was
        // being returned regardless of status, causing stale plan data to show in the UI.
        return subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartsAt)
            .FirstOrDefault();
    }

    private static SubscriptionResponse MapSubscription(Subscription subscription)
    {
        var daysUntilExpiration = Math.Max(0, (int)Math.Ceiling((subscription.EndsAt - DateTime.UtcNow).TotalDays));
        var isExpiringSoon = subscription.Status == SubscriptionStatus.Active && daysUntilExpiration <= 7;
        var notice = isExpiringSoon
            ? $"Your {subscription.Tier} subscription expires in {daysUntilExpiration} day{(daysUntilExpiration == 1 ? string.Empty : "s")}."
            : null;

        return new SubscriptionResponse(
            subscription.Id,
            subscription.Tier,
            subscription.Duration,
            subscription.Status,
            subscription.Price,
            subscription.Currency,
            subscription.EndsAt,
            isExpiringSoon,
            daysUntilExpiration,
            notice);
    }

    private static string BuildDemoCustomerId(Guid userId)
    {
        return $"cus_demo_{userId:N}"[..21];
    }
}
