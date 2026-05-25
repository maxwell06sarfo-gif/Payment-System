using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentSystem.Core.CQRS.Authentication;
using PaymentSystem.Core.Entities;
using PaymentSystem.Infrastructure.Data;
using PaymentSystem.Infrastructure.Services;

namespace PaymentSystem.Infrastructure.CQRS.Handlers;

public class AuthHandler :
    IRequestHandler<RegisterUserCommand, RegistrationResult>,
    IRequestHandler<LoginUserQuery, AuthTokenResult>,
    IRequestHandler<RefreshTokenCommand, AuthTokenResult>
{
    private readonly IAppDataStore _dataStore;
    private readonly AuthService _authService;
    private readonly ILogger<AuthHandler> _logger;

    public AuthHandler(IAppDataStore dataStore, AuthService authService, ILogger<AuthHandler> logger)
    {
        _dataStore = dataStore;
        _authService = authService;
        _logger = logger;
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
        try
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _dataStore.GetUserByEmailAsync(normalizedEmail, ct);

            if (user == null || !_authService.VerifyPassword(request.Password, user.PasswordHash))
                return new AuthTokenResult(false, null, null);

            var jwt = _authService.GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken(user.Id);

            await _dataStore.SaveRefreshTokenAsync(refreshToken, ct);

            return new AuthTokenResult(true, jwt, refreshToken.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
            return new AuthTokenResult(false, null, null);
        }
    }

    public async Task<AuthTokenResult> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var storedToken = await _dataStore.GetRefreshTokenAsync(request.Token, ct);

        if (storedToken == null || !storedToken.IsActive)
            return new AuthTokenResult(false, null, null);

        var user = await _dataStore.GetUserWithSubscriptionsAsync(storedToken.UserId, ct);
        if (user == null) return new AuthTokenResult(false, null, null);

        // Revoke old token
        storedToken.RevokedAt = DateTime.UtcNow;
        await _dataStore.SaveRefreshTokenAsync(storedToken, ct);

        // Generate new pair
        var jwt = _authService.GenerateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken(user.Id);
        await _dataStore.SaveRefreshTokenAsync(newRefreshToken, ct);

        return new AuthTokenResult(true, jwt, newRefreshToken.Token);
    }

    private static RefreshToken GenerateRefreshToken(Guid userId) => new()
    {
        Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64)),
        UserId = userId,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        CreatedAt = DateTime.UtcNow
    };
}
