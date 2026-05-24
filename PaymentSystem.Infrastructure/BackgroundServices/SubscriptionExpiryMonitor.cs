using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentSystem.Infrastructure.Data;

namespace PaymentSystem.Infrastructure.BackgroundServices;

public class SubscriptionExpiryMonitor : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SubscriptionExpiryMonitor> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromDays(1); // Runs once every day

    public SubscriptionExpiryMonitor(IServiceProvider services, ILogger<SubscriptionExpiryMonitor> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Subscription Expiry Monitor Service starting up...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Scanning database for expired subscriptions...");
                await CheckAndExpireSubscriptionsAsync(stoppingToken);
                await MarkSubscriptionsForExpiryNotificationAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing subscription expirations.");
            }

            // Wait for 24 hours or until the application shuts down
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckAndExpireSubscriptionsAsync(CancellationToken ct)
    {
        // Since BackgroundServices are singletons, we must create a scope to inject a scoped AppDbContext
        using var scope = _services.CreateScope();
        var dataStore = scope.ServiceProvider.GetRequiredService<IAppDataStore>();

        var now = DateTime.UtcNow;

        // Fetch active/past-due items that have breached their expiration threshold
        var expiredSubscriptions = (await dataStore.GetSubscriptionsToExpireAsync(now, ct)).ToList();

        if (expiredSubscriptions.Count > 0)
        {
            foreach (var sub in expiredSubscriptions)
            {
                _logger.LogInformation("Subscription {SubId} for User {UserId} has passed its end date. Flagging as Expired.", sub.Id, sub.UserId);
            }

            await dataStore.MarkSubscriptionsExpiredAsync(expiredSubscriptions.Select(subscription => subscription.Id), ct);
            _logger.LogInformation("Successfully updated {Count} expired subscriptions.", expiredSubscriptions.Count);
        }
        else
        {
            _logger.LogTrace("No expired subscriptions found.");
        }
    }

    private async Task MarkSubscriptionsForExpiryNotificationAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var dataStore = scope.ServiceProvider.GetRequiredService<IAppDataStore>();

        var now = DateTime.UtcNow;
        var notificationWindow = now.AddDays(7);

        var expiringSubscriptions = (await dataStore.GetSubscriptionsForExpirationNoticeAsync(
            now,
            notificationWindow,
            ct)).ToList();

        if (expiringSubscriptions.Count > 0)
        {
            foreach (var sub in expiringSubscriptions)
            {
                _logger.LogInformation(
                    "Subscription {SubId} for User {UserId} is about to expire on {EndsAt}. Notification marked.",
                    sub.Id,
                    sub.UserId,
                    sub.EndsAt);
            }

            await dataStore.MarkExpirationNotificationsSentAsync(
                expiringSubscriptions.Select(subscription => subscription.Id),
                now,
                ct);
        }
        else
        {
            _logger.LogTrace("No subscriptions requiring expiry notifications were found.");
        }
    }
}
