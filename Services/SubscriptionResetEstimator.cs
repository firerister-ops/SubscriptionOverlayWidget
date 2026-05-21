using System;
using System.Collections.Generic;
using SubscriptionOverlayWidget.Models;

namespace SubscriptionOverlayWidget.Services;

public sealed class SubscriptionResetEstimator
{
    private static readonly Dictionary<string, TimeSpan> Intervals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["free"] = TimeSpan.FromHours(10),
        ["promo"] = TimeSpan.FromHours(8),
        ["simple"] = TimeSpan.FromHours(5),
        ["payed"] = TimeSpan.FromHours(4),
        ["wormsoft developer"] = TimeSpan.FromHours(2),
        ["wormsoft boss"] = TimeSpan.FromHours(1)
    };

    public void Apply(AppSettings settings, SubscriptionLimitInfo info)
    {
        if (!info.IsSuccess || string.IsNullOrWhiteSpace(info.SubscriptionType) || !info.RemainingLimitValue.HasValue)
        {
            info.ResetCountdownText = "Сброс лимитов: недоступен";
            return;
        }

        if (!Intervals.TryGetValue(info.SubscriptionType, out var interval))
        {
            info.ResetCountdownText = "Сброс лимитов: неизвестный тариф";
            return;
        }

        var observedAtUtc = info.ServerDateUtc ?? info.UpdatedAt?.ToUniversalTime() ?? DateTime.UtcNow;
        var remaining = info.RemainingLimitValue.Value;
        var samePlan = string.Equals(settings.LastKnownSubscriptionType, info.SubscriptionType, StringComparison.OrdinalIgnoreCase);
        var previousLimit = settings.LastKnownRemainingLimit;
        var limitIncreased = samePlan && previousLimit >= 0 && remaining > previousLimit;

        DateTime lastResetUtc;
        if (!samePlan || !settings.LastResetAtUtc.HasValue)
        {
            lastResetUtc = observedAtUtc;
        }
        else if (limitIncreased)
        {
            lastResetUtc = observedAtUtc;
        }
        else
        {
            lastResetUtc = settings.LastResetAtUtc.Value;
            while (lastResetUtc + interval <= observedAtUtc)
            {
                lastResetUtc += interval;
            }
        }

        var nextResetUtc = lastResetUtc + interval;
        while (nextResetUtc <= observedAtUtc)
        {
            lastResetUtc = nextResetUtc;
            nextResetUtc = lastResetUtc + interval;
        }

        info.ResetInterval = interval;
        info.EstimatedLastResetUtc = lastResetUtc;
        info.EstimatedNextResetUtc = nextResetUtc;
        info.ResetCountdownText = $"Сброс лимитов: {Format(nextResetUtc - observedAtUtc)}";

        settings.LastKnownSubscriptionType = info.SubscriptionType;
        settings.LastKnownRemainingLimit = remaining;
        settings.LastObservedAtUtc = observedAtUtc;
        settings.LastResetAtUtc = lastResetUtc;
    }

    public string GetLiveCountdown(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.LastKnownSubscriptionType) || !settings.LastResetAtUtc.HasValue)
        {
            return "Сброс лимитов: Жду ресета";
        }

        if (!Intervals.TryGetValue(settings.LastKnownSubscriptionType, out var interval))
        {
            return "Сброс лимитов: неизвестный тариф";
        }

        var nextResetUtc = settings.LastResetAtUtc.Value + interval;
        while (nextResetUtc <= DateTime.UtcNow)
        {
            nextResetUtc += interval;
        }

        return $"Сброс лимитов: {Format(nextResetUtc - DateTime.UtcNow)}";
    }

    private static string Format(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }
}
