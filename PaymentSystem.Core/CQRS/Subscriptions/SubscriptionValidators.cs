using FluentValidation;

namespace PaymentSystem.Core.CQRS.Subscriptions;

public class GetUserProfileQueryValidator : AbstractValidator<GetUserProfileQuery>
{
    public GetUserProfileQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Authenticated user context is required.");
    }
}

public class CreateSubscriptionCommandValidator : AbstractValidator<CreateSubscriptionCommand>
{
    public CreateSubscriptionCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Authenticated user context is required.");
        RuleFor(x => x.Tier).IsInEnum().WithMessage("Invalid subscription tier requested.");
        RuleFor(x => x.Duration).IsInEnum().WithMessage("Invalid subscription duration requested.");
        RuleFor(x => x.SuccessUrl).NotEmpty().WithMessage("Success redirect URL is required.");
        RuleFor(x => x.CancelUrl).NotEmpty().WithMessage("Cancel redirect URL is required.");
    }
}

public class ActivateSubscriptionFromStripeCommandValidator : AbstractValidator<ActivateSubscriptionFromStripeCommand>
{
    public ActivateSubscriptionFromStripeCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Stripe webhook user context is required.");
    }
}
