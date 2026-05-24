using Microsoft.EntityFrameworkCore;
using PaymentSystem.Core.Entities;

namespace PaymentSystem.Infrastructure.Data;

public class EfAppDataStore : IAppDataStore
{
    private readonly AppDbContext _db;

    public EfAppDataStore(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> UserExistsByEmailAsync(string email, CancellationToken ct)
    {
        return _db.Users.AnyAsync(user => user.Email == email, ct);
    }

    public Task<User?> GetUserByEmailAsync(string email, CancellationToken ct)
    {
        return _db.Users.FirstOrDefaultAsync(user => user.Email == email, ct);
    }

    public Task<User?> GetUserWithSubscriptionsAsync(Guid userId, CancellationToken ct)
    {
        return _db.Users
            .Include(user => user.Subscriptions)
            .FirstOrDefaultAsync(user => user.Id == userId, ct);
    }

    public async Task AddUserAsync(User user, CancellationToken ct)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
    }

    public Task UpdateUserAsync(User user, CancellationToken ct)
    {
        return _db.SaveChangesAsync(ct);
    }

    public async Task MarkActiveSubscriptionsReplacedAsync(Guid userId, CancellationToken ct)
    {
        var subscriptions = await _db.Subscriptions
            .Where(subscription => subscription.UserId == userId && subscription.Status == "Active")
            .ToListAsync(ct);

        foreach (var subscription in subscriptions)
        {
            subscription.Status = "Replaced";
        }

        if (subscriptions.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task AddSubscriptionAsync(Subscription subscription, CancellationToken ct)
    {
        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<Subscription>> GetSubscriptionsToExpireAsync(DateTime now, CancellationToken ct)
    {
        return await _db.Subscriptions
            .Where(subscription => subscription.Status != "Expired"
                && subscription.Status != "Canceled"
                && subscription.EndsAt < now)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<Subscription>> GetSubscriptionsForExpirationNoticeAsync(
        DateTime now,
        DateTime notificationWindow,
        CancellationToken ct)
    {
        return await _db.Subscriptions
            .Where(subscription => subscription.Status == "Active"
                && subscription.EndsAt >= now
                && subscription.EndsAt <= notificationWindow
                && (subscription.LastExpirationNotificationAt == null
                    || subscription.LastExpirationNotificationAt < now.AddDays(-1)))
            .ToListAsync(ct);
    }

    public async Task MarkSubscriptionsExpiredAsync(IEnumerable<Guid> subscriptionIds, CancellationToken ct)
    {
        var ids = subscriptionIds.ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var subscriptions = await _db.Subscriptions
            .Where(subscription => ids.Contains(subscription.Id))
            .ToListAsync(ct);

        foreach (var subscription in subscriptions)
        {
            subscription.Status = "Expired";
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkExpirationNotificationsSentAsync(
        IEnumerable<Guid> subscriptionIds,
        DateTime sentAt,
        CancellationToken ct)
    {
        var ids = subscriptionIds.ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var subscriptions = await _db.Subscriptions
            .Where(subscription => ids.Contains(subscription.Id))
            .ToListAsync(ct);

        foreach (var subscription in subscriptions)
        {
            subscription.LastExpirationNotificationAt = sentAt;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ActivateLatestPendingCheckoutAsync(
        Guid userId,
        string? stripeSubscriptionId,
        CancellationToken ct)
    {
        var subscription = await _db.Subscriptions
            .Where(item => item.UserId == userId && item.Status == "PendingCheckout")
            .OrderByDescending(item => item.StartsAt)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
        {
            return;
        }

        // Ensure old subscriptions are marked as replaced now that the new one is confirmed
        await MarkActiveSubscriptionsReplacedAsync(userId, ct);

        subscription.Status = "Active";
        subscription.StripeSubscriptionId = stripeSubscriptionId ?? subscription.StripeSubscriptionId;
        await _db.SaveChangesAsync(ct);
    }
}
