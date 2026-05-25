using FluentValidation;

namespace PaymentSystem.Core.CQRS.Subscriptions;

public class GetUserProfileQueryValidator : AbstractValidator<GetUserProfileQuery>
{
    public GetUserProfileQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User context is required.");
    }
}

public class CreateSubscriptionCommandValidator : AbstractValidator<CreateSubscriptionCommand>
{
    public CreateSubscriptionCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User context is required.");
        RuleFor(x => x.Tier).IsInEnum().WithMessage("Invalid subscription tier.");
        RuleFor(x => x.Duration).IsInEnum().WithMessage("Invalid subscription duration.");

        RuleFor(x => x.SuccessUrl)
            .NotEmpty().WithMessage("Success redirect URL is required.")
            .Must(BeAValidHttpsUrl).WithMessage("Success URL must be a valid absolute URL.");

        RuleFor(x => x.CancelUrl)
            .NotEmpty().WithMessage("Cancel redirect URL is required.")
            .Must(BeAValidHttpsUrl).WithMessage("Cancel URL must be a valid absolute URL.");
    }

    private static bool BeAValidHttpsUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }
}

public class ActivateSubscriptionFromStripeCommandValidator : AbstractValidator<ActivateSubscriptionFromStripeCommand>
{
    public ActivateSubscriptionFromStripeCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Stripe webhook user context is required.");
    }
}
