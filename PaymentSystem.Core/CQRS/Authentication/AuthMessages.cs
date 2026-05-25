using MediatR;
using PaymentSystem.Core.DTOs;

namespace PaymentSystem.Core.CQRS.Authentication;

// Triggers user registration — mutates state, so it's a Command not a Query.
public record RegisterUserCommand(string Email, string Password, string FullName) : IRequest<RegistrationResult>;

// Validates credentials and issues both a JWT and a refresh token.
public record LoginUserQuery(string Email, string Password) : IRequest<AuthTokenResult>;

// Validates an existing refresh token and rotates it, issuing a new JWT + refresh token pair.
public record RefreshTokenCommand(string Token) : IRequest<AuthTokenResult>;

// Revokes a refresh token so it can never be used again (logout).
public record RevokeTokenCommand(string Token) : IRequest<bool>;

// Immutable result records. Handlers return these; callers must not mutate them.
public record RegistrationResult(bool IsSuccess, string Message);
public record AuthTokenResult(bool IsAuthenticated, string? Token, string? RefreshToken);
