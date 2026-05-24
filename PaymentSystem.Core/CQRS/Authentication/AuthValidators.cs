using FluentValidation;

namespace PaymentSystem.Core.CQRS.Authentication;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is mandatory.")
            .EmailAddress().WithMessage("A valid email formatting structure is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password field cannot be evaluated empty.")
            .MinimumLength(8).WithMessage("Security standard requires a minimum password length of 8 characters.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name descriptor is required.")
            .MaximumLength(100).WithMessage("Name entry length exceeded corporate ledger limit.");
    }
}

public class LoginUserQueryValidator : AbstractValidator<LoginUserQuery>
{
    public LoginUserQueryValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email routing parameter is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Security credentials must be supplied.");
    }
}
