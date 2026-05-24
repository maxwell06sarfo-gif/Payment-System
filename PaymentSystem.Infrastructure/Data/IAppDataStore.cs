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
    Task<IReadOnlyCollection<Subscription>> GetSubscriptionsToExpireAsync(DateTime now, CancellationToken ct);
    Task<IReadOnlyCollection<Subscription>> GetSubscriptionsForExpirationNoticeAsync(
        DateTime now,
        DateTime notificationWindow,
        CancellationToken ct);
    Task MarkSubscriptionsExpiredAsync(IEnumerable<Guid> subscriptionIds, CancellationToken ct);
    Task MarkExpirationNotificationsSentAsync(IEnumerable<Guid> subscriptionIds, DateTime sentAt, CancellationToken ct);
    Task ActivateLatestPendingCheckoutAsync(Guid userId, string? stripeSubscriptionId, CancellationToken ct);
}
