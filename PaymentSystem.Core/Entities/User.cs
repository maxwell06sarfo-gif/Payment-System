using System;
using System.Collections.Generic;

namespace PaymentSystem.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? StripeCustomerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Relational Navigation properties
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
