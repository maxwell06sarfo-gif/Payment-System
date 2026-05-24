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
        if (await _dataStore.UserExistsByEmailAsync(request.Email, ct))
            return new RegistrationResult(false, "User already exists.");

        var user = new User
        {
            Email = request.Email,
            FullName = request.FullName,
            PasswordHash = _authService.HashPassword(request.Password)
        };

        await _dataStore.AddUserAsync(user, ct);
        return new RegistrationResult(true, "User created.");
    }

    public async Task<AuthTokenResult> Handle(LoginUserQuery request, CancellationToken ct)
    {
        var user = await _dataStore.GetUserByEmailAsync(request.Email, ct);
        if (user == null || !_authService.VerifyPassword(request.Password, user.PasswordHash))
            return new AuthTokenResult(false, null);

        return new AuthTokenResult(true, _authService.GenerateJwtToken(user));
    }
}
