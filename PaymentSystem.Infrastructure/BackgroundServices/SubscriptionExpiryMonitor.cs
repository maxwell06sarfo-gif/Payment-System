using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PaymentSystem.Infrastructure.Data;

namespace PaymentSystem.Infrastructure.BackgroundServices;

public class SubscriptionExpiryMonitor : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SubscriptionExpiryMonitor> _logger;
    private readonly TimeSpan _checkInterval;

    public SubscriptionExpiryMonitor(
        IServiceProvider services, 
        ILogger<SubscriptionExpiryMonitor> logger, 
        IConfiguration configuration)
    {
        _services = services;
        _logger = logger;

        // Interval is configurable so it can be shortened in staging without a code change.
        // Defaults to 24 hours if the key is absent from configuration.
        var rawInterval = configuration["SubscriptionExpiryMonitor:CheckIntervalHours"];
        _checkInterval = TimeSpan.FromHours(int.TryParse(rawInterval, out var hours) ? hours : 24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Subscription expiry monitor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Scanning for subscriptions past their end date...");

                // A new scope per cycle keeps the DbContext lifetime aligned with
                // a single unit of work rather than the lifetime of the host process.
                using var scope = _services.CreateScope();
                var dataStore = scope.ServiceProvider.GetRequiredService<IAppDataStore>();
                
                var now = DateTime.UtcNow;
                int expiredCount = await dataStore.ExpireAllPassedSubscriptionsAsync(now, stoppingToken);
                int notifiedCount = await dataStore.SendBulkExpirationNotificationsAsync(now, now.AddDays(7), stoppingToken);

                if (expiredCount > 0) _logger.LogInformation("Marked {Count} subscription(s) as expired.", expiredCount);
                if (notifiedCount > 0) _logger.LogInformation("Queued expiration notices for {Count} user(s).", notifiedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in subscription expiry monitor.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
