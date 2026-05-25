namespace PaymentSystem.Core.Constants;

/// <summary>
/// Canonical status values for a subscription record.
///
/// Centralising these as typed constants rather than scattering raw strings
/// across the codebase means a typo fails at compile time, not silently at
/// runtime during a state transition. Any new status belongs here first.
/// </summary>
public static class SubscriptionStatus
{
    /// <summary>The subscription is in good standing and grants access to the subscribed tier.</summary>
    public const string Active = "Active";

    /// <summary>
    /// The record exists but has never been activated.
    /// Typical for accounts created before a first payment is confirmed.
    /// </summary>
    public const string Inactive = "Inactive";

    /// <summary>
    /// The billing period ended without renewal.
    /// Written exclusively by the background expiry monitor — never set by hand.
    /// </summary>
    public const string Expired = "Expired";

    /// <summary>The user cancelled before the natural end date. Retained for audit and refund reference.</summary>
    public const string Canceled = "Canceled";

    /// <summary>
    /// This subscription was superseded when the user purchased a new plan mid-cycle.
    /// Kept so billing history remains complete and attributable.
    /// </summary>
    public const string Replaced = "Replaced";

    /// <summary>
    /// A Stripe Checkout session has been opened but the user has not completed payment yet.
    /// If the session lapses, the expiry monitor will clean this up like any other stale record.
    /// </summary>
    public const string PendingCheckout = "PendingCheckout";
}
