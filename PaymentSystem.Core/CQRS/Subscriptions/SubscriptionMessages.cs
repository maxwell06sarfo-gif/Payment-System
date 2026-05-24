using MediatR;
using PaymentSystem.Core.DTOs;
using PaymentSystem.Core.Enums;

namespace PaymentSystem.Core.CQRS.Subscriptions;

public record GetSubscriptionPlansQuery() : IRequest<IReadOnlyCollection<SubscriptionPlanResponse>>;

public record GetUserProfileQuery(Guid UserId) : IRequest<UserProfileResponse>;

public record CreateSubscriptionCommand(
    Guid UserId,
    SubscriptionTier Tier,
    SubscriptionDuration Duration,
    string SuccessUrl,
    string CancelUrl
) : IRequest<CreateSubscriptionResponse>;

public record ActivateSubscriptionFromStripeCommand(
    Guid UserId,
    string? StripeSubscriptionId
) : IRequest<bool>;
