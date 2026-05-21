using System;
using System.Collections.Generic;
using SubscriptionOverlayWidget.Models;

namespace SubscriptionOverlayWidget.Services;

public sealed class SubscriptionResetEstimator
{
    private static readonly List<(string keyword, TimeSpan interval)> Tiers = new()
    {
        ("free", TimeSpan.FromHours(10)),
        ("promo", TimeSpan.FromHours(8)),
        ("simple", TimeSpan.FromHours(5)),
        ("payed", TimeSpan.FromHours(4)),
        ("developer", TimeSpan.FromHours(2)),
        ("boss", TimeSpan.FromHours(1))
    };

    private readonly DebugLogService _log = new();

    private static string Normalize(string s) => s.Replace(" ", "").Replace("-", "").Replace("_", "").ToUpperInvariant();

    private static bool TryMatchTier(string subscriptionType, out TimeSpan interval)
    {
        var normalized = Normalize(subscriptionType);
        foreach (var (keyword, span) in Tiers)
        {
            if (normalized.Contains(keyword.ToUpperInvariant(), StringComparison.Ordinal))
            {
                interval = span;
                return true;
            }
        }
        interval = default;
        return false;
    }

    public void Apply(AppSettings settings, SubscriptionLimitInfo info)
    {
        if (!info.IsSuccess || string.IsNullOrWhiteSpace(info.SubscriptionType) || !info.RemainingLimitValue.HasValue)
        {
            _ = _log.WriteAsync($"[Reset] SKIP: IsSuccess={info.IsSuccess}, Type={info.SubscriptionType}, Raw={info.RemainingLimit}, Value={info.RemainingLimitValue}");
            info.ResetCountdownText = "Сброс лимитов: недоступен";
            return;
        }

        if (!TryMatchTier(info.SubscriptionType, out var interval))
        {
            info.ResetCountdownText = "Сброс лимитов: неизвестный тариф";
            settings.LastKnownSubscriptionType = info.SubscriptionType;
            settings.LastKnownRemainingLimit = info.RemainingLimitValue.Value;
            _ = _log.WriteAsync($"[Reset] UNKNOWN TIER: {info.SubscriptionType}");
            return;
        }

        var remaining = info.RemainingLimitValue.Value;
        var previousLimit = settings.LastKnownRemainingLimit;

        _ = _log.WriteAsync($"[Reset] now={remaining}, prev={previousLimit}, type={info.SubscriptionType}");

        // Единственный триггер: лимит вырос → запускаем таймер
        if (previousLimit >= 0 && remaining > previousLimit)
        {
            _ = _log.WriteAsync($"[Reset] DETECTED! {remaining} > {previousLimit} → timer {interval.TotalHours}h");
            settings.LastResetAtUtc = DateTime.UtcNow;
        }

        // Кешируем текущий лимит
        settings.LastKnownSubscriptionType = info.SubscriptionType;
        settings.LastKnownRemainingLimit = remaining;

        // Показываем таймер
        if (settings.LastResetAtUtc.HasValue)
        {
            var left = settings.LastResetAtUtc.Value + interval - DateTime.UtcNow;
            if (left > TimeSpan.Zero)
            {
                info.ResetCountdownText = $"Сброс лимитов: {Format(left)}";
                return;
            }
        }

        info.ResetCountdownText = "Сброс лимитов: Жду ресета";
    }

    public string GetLiveCountdown(AppSettings settings)
    {
        if (!settings.LastResetAtUtc.HasValue)
            return "Сброс лимитов: Жду ресета";

        if (!TryMatchTier(settings.LastKnownSubscriptionType, out var interval))
            return "Сброс лимитов: неизвестный тариф";

        var left = settings.LastResetAtUtc.Value + interval - DateTime.UtcNow;
        if (left <= TimeSpan.Zero)
            return "Сброс лимитов: Жду ресета";

        return $"Сброс лимитов: {Format(left)}";
    }

    private static string Format(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }
}
