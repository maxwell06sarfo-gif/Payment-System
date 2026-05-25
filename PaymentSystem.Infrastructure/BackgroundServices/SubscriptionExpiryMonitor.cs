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
        _checkInterval = TimeSpan.FromHours(configuration.GetValue("SubscriptionExpiryMonitor:CheckIntervalHours", 24));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Subscription Expiry Monitor Service starting up...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Scanning database for expired subscriptions...");
                using var scope = _services.CreateScope();
                var dataStore = scope.ServiceProvider.GetRequiredService<IAppDataStore>();
                
                var now = DateTime.UtcNow;
                int expiredCount = await dataStore.ExpireAllPassedSubscriptionsAsync(now, stoppingToken);
                int notifiedCount = await dataStore.SendBulkExpirationNotificationsAsync(now, now.AddDays(7), stoppingToken);

                if (expiredCount > 0) _logger.LogInformation("Expired {Count} subscriptions.", expiredCount);
                if (notifiedCount > 0) _logger.LogInformation("Notified {Count} users of upcoming expiry.", notifiedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing subscription expirations.");
            }

            // Wait for 24 hours or until the application shuts down
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
