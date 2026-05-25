using Microsoft.EntityFrameworkCore;
using PaymentSystem.Core.Constants;
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
        await _db.Subscriptions
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Status, SubscriptionStatus.Replaced), ct);
    }

    public async Task AddSubscriptionAsync(Subscription subscription, CancellationToken ct)
    {
        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ExpireAllPassedSubscriptionsAsync(DateTime now, CancellationToken ct)
    {
        return await _db.Subscriptions
            .Where(s => s.Status != SubscriptionStatus.Expired
                     && s.Status != SubscriptionStatus.Canceled
                     && s.EndsAt < now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Status, SubscriptionStatus.Expired), ct);
    }

    public async Task<int> SendBulkExpirationNotificationsAsync(DateTime now, DateTime window, CancellationToken ct)
    {
        return await _db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active
                && s.EndsAt >= now
                && s.EndsAt <= window
                && (s.LastExpirationNotificationAt == null || s.LastExpirationNotificationAt < now.AddDays(-1)))
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.LastExpirationNotificationAt, now), ct);
    }

    public async Task ActivateLatestPendingCheckoutAsync(
        Guid userId,
        string? stripeSubscriptionId,
        CancellationToken ct)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var subscription = await _db.Subscriptions
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.PendingCheckout)
                .OrderByDescending(s => s.StartsAt)
                .FirstOrDefaultAsync(ct);

            if (subscription is null)
                return;

            await MarkActiveSubscriptionsReplacedAsync(userId, ct);

            subscription.Status = SubscriptionStatus.Active;
            subscription.StripeSubscriptionId = stripeSubscriptionId ?? subscription.StripeSubscriptionId;

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
