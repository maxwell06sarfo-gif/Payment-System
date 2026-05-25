using MediatR;
using PaymentSystem.Core.DTOs;

namespace PaymentSystem.Core.CQRS.Authentication;

// Triggers user registration — mutates state, so it's a Command not a Query.
public record RegisterUserCommand(string Email, string Password, string FullName) : IRequest<RegistrationResult>;

// Validates credentials and issues a token — read-only, so modelled as a Query.
public record LoginUserQuery(string Email, string Password) : IRequest<AuthTokenResult>;

// Immutable result records. Handlers return these; callers must not mutate them.
public record RegistrationResult(bool IsSuccess, string Message);
public record AuthTokenResult(bool IsAuthenticated, string? Token);
