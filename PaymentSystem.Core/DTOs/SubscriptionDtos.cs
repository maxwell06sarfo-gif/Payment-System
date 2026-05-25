using System;
using PaymentSystem.Core.Enums;

namespace PaymentSystem.Core.DTOs;

// ── Request Records ──────────────────────────────────────────────────────────
public record UserRegisterRequest(string Email, string Password, string FullName);
public record UserLoginRequest(string Email, string Password);
public record CreateSubscriptionRequest(SubscriptionTier Tier, SubscriptionDuration Duration);
public record RefreshTokenRequest(string Token);

// ── Response Records ─────────────────────────────────────────────────────────
public record UserResponse(Guid Id, string Email, string FullName, string? StripeCustomerId);

public record SubscriptionResponse(
    Guid Id,
    SubscriptionTier Tier,
    SubscriptionDuration Duration,
    SubscriptionStatus Status,
    decimal Price,
    string Currency,
    DateTime EndsAt,
    bool IsExpiringSoon,
    int DaysUntilExpiration,
    string? ExpirationNotice
);

public record SubscriptionPlanResponse(
    SubscriptionTier Tier,
    string Name,
    string Description,
    decimal MonthlyPrice,
    decimal SixMonthPrice,
    decimal YearlyPrice,
    string[] Features
);

public record UserProfileResponse(
    UserResponse User,
    SubscriptionResponse? ActiveSubscription
);

public record CreateSubscriptionResponse(
    bool IsSuccess,
    string Message,
    string? CheckoutUrl,
    SubscriptionResponse? Subscription
);

public record BillingPortalResponse(string Url);
