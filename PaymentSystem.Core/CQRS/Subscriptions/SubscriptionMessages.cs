using MediatR;
using PaymentSystem.Core.DTOs;
using PaymentSystem.Core.Enums;

namespace PaymentSystem.Core.CQRS.Subscriptions;

// Read-only — no user context needed; plan catalogue is the same for everyone.
public record GetSubscriptionPlansQuery() : IRequest<IReadOnlyCollection<SubscriptionPlanResponse>>;

// Fetches the full profile for a specific authenticated user, including their active subscription.
public record GetUserProfileQuery(Guid UserId) : IRequest<UserProfileResponse>;

// Kicks off a new subscription purchase. If Stripe is configured, a Checkout session
// is created and the record lands in PendingCheckout until the webhook confirms payment.
public record CreateSubscriptionCommand(
    Guid UserId,
    SubscriptionTier Tier,
    SubscriptionDuration Duration,
    string SuccessUrl,
    string CancelUrl
) : IRequest<CreateSubscriptionResponse>;

// Fired by the Stripe webhook handler once checkout.session.completed is received.
// Moves the matching PendingCheckout record to Active and stamps the Stripe subscription ID.
public record ActivateSubscriptionFromStripeCommand(
    Guid UserId,
    string? StripeSubscriptionId
) : IRequest<bool>;
