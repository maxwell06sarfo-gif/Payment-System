using PaymentSystem.Core.Entities;

namespace PaymentSystem.Infrastructure.Data;

public interface IAppDataStore
{
    Task<bool> UserExistsByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetUserByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetUserWithSubscriptionsAsync(Guid userId, CancellationToken ct);
    Task AddUserAsync(User user, CancellationToken ct);
    Task UpdateUserAsync(User user, CancellationToken ct);
    Task MarkActiveSubscriptionsReplacedAsync(Guid userId, CancellationToken ct);
    Task AddSubscriptionAsync(Subscription subscription, CancellationToken ct);
    Task<int> ExpireAllPassedSubscriptionsAsync(DateTime now, CancellationToken ct);
    Task<int> SendBulkExpirationNotificationsAsync(DateTime now, DateTime window, CancellationToken ct);
    Task ActivateLatestPendingCheckoutAsync(Guid userId, string? stripeSubscriptionId, CancellationToken ct);

    // Idempotency
    Task<bool> HasWebhookBeenProcessedAsync(string eventId, CancellationToken ct);
    Task MarkWebhookAsProcessedAsync(string eventId, CancellationToken ct);

    // Refresh Tokens
    Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct);
    Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct);
}
