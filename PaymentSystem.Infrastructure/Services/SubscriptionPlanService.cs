using System;
using System.Collections.Generic;
using PaymentSystem.Core.DTOs;
using PaymentSystem.Core.Enums;

namespace PaymentSystem.Infrastructure.Services;

public class SubscriptionPlanService
{
    private static readonly Dictionary<SubscriptionTier, decimal> PlanRates = new()
    {
        { SubscriptionTier.Promotion, 19.99m },
        { SubscriptionTier.Gold, 49.99m },
        { SubscriptionTier.Diamond, 99.99m }
    };

    public (bool IsValid, string ErrorMessage) ValidateSubscriptionRequest(CreateSubscriptionRequest request)
    {
        if (!Enum.IsDefined(typeof(SubscriptionTier), request.Tier))
        {
            return (false, "Invalid subscription tier requested.");
        }

        if (!Enum.IsDefined(typeof(SubscriptionDuration), request.Duration))
        {
            return (false, "Invalid duration specified. Must be Monthly, SixMonths, or Yearly.");
        }

        return (true, string.Empty);
    }

    public decimal GetPlanPrice(SubscriptionTier tier, SubscriptionDuration duration)
    {
        if (!PlanRates.TryGetValue(tier, out var baseRate))
            throw new ArgumentException("Specified plan tier does not exist in rate configurations.");

        return duration switch
        {
            SubscriptionDuration.Monthly => baseRate,
            SubscriptionDuration.SixMonths => Math.Round(baseRate * 6 * 0.90m, 2),
            SubscriptionDuration.Yearly => Math.Round(baseRate * 12 * 0.80m, 2),
            _ => throw new ArgumentException("Specified subscription duration does not exist in rate configurations.")
        };
    }
}
