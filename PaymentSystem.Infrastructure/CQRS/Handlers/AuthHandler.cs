using MediatR;
using PaymentSystem.Core.CQRS.Authentication;
using PaymentSystem.Core.Entities;
using PaymentSystem.Infrastructure.Data;
using PaymentSystem.Infrastructure.Services;

namespace PaymentSystem.Infrastructure.CQRS.Handlers;

public class AuthHandler :
    IRequestHandler<RegisterUserCommand, RegistrationResult>,
    IRequestHandler<LoginUserQuery, AuthTokenResult>
{
    private readonly IAppDataStore _dataStore;
    private readonly AuthService _authService;

    public AuthHandler(IAppDataStore dataStore, AuthService authService)
    {
        _dataStore = dataStore;
        _authService = authService;
    }

    public async Task<RegistrationResult> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await _dataStore.UserExistsByEmailAsync(normalizedEmail, ct))
            return new RegistrationResult(false, "An account with that email already exists.");

        var user = new User
        {
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = _authService.HashPassword(request.Password)
        };

        await _dataStore.AddUserAsync(user, ct);
        return new RegistrationResult(true, "Account created successfully.");
    }

    public async Task<AuthTokenResult> Handle(LoginUserQuery request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dataStore.GetUserByEmailAsync(normalizedEmail, ct);

        if (user == null || !_authService.VerifyPassword(request.Password, user.PasswordHash))
            return new AuthTokenResult(false, null);

        return new AuthTokenResult(true, _authService.GenerateJwtToken(user));
    }
}
