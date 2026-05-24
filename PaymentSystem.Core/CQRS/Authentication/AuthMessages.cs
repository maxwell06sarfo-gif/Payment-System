using MediatR;
using PaymentSystem.Core.DTOs;

namespace PaymentSystem.Core.CQRS.Authentication;

// Command: Represents a data-write action to register a new system user
public record RegisterUserCommand(string Email, string Password, string FullName) : IRequest<RegistrationResult>;

// Query: Represents a data-read action to validate credentials and issue a token
public record LoginUserQuery(string Email, string Password) : IRequest<AuthTokenResult>;

// Clean, immutable output response records for our handlers
public record RegistrationResult(bool IsSuccess, string Message);
public record AuthTokenResult(bool IsAuthenticated, string? Token);
