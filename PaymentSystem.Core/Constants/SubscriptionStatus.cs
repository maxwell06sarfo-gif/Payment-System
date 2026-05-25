namespace PaymentSystem.Core.Constants;

/// <summary>
/// Well-known subscription status values used across the domain and data layers.
/// Using this class prevents silent typos from silently breaking state transitions.
/// </summary>
public static class SubscriptionStatus
{
    /// <summary>The subscription is current and grants access to the subscribed tier.</summary>
    public const string Active = "Active";

    /// <summary>The subscription record exists but has not been activated yet.</summary>
    public const string Inactive = "Inactive";

    /// <summary>The subscription period has passed its end date and was not renewed.</summary>
    public const string Expired = "Expired";

    /// <summary>The subscription was cancelled before its natural end date.</summary>
    public const string Canceled = "Canceled";

    /// <summary>The subscription was superseded by a newer subscription for the same user.</summary>
    public const string Replaced = "Replaced";

    /// <summary>A Stripe checkout session was created but the user has not yet completed payment.</summary>
    public const string PendingCheckout = "PendingCheckout";
}
