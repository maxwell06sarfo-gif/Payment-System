using System;
using PaymentSystem.Core.Enums;

namespace PaymentSystem.Core.Entities;

public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public SubscriptionTier Tier { get; set; }
    public SubscriptionDuration Duration { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string Status { get; set; } = "Inactive";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime StartsAt { get; set; } = DateTime.UtcNow;
    public DateTime EndsAt { get; set; }
    public bool IsAutoRenewEnabled { get; set; } = true;
    public DateTime? LastExpirationNotificationAt { get; set; }

    // Relational Navigation properties
    public User? User { get; set; }
}
