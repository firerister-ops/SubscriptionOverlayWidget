namespace SubscriptionOverlayWidget.Models;

public sealed class SubscriptionLimitInfo
{
    public bool IsSuccess { get; set; }

    public string SubscriptionType { get; set; } = "—";

    public string RemainingLimit { get; set; } = "—";

    public string StatusText { get; set; } = "Инициализация...";

    public string RawResponse { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ServerDateUtc { get; set; }

    public long? RemainingLimitValue { get; set; }

    public TimeSpan? ResetInterval { get; set; }

    public DateTime? EstimatedLastResetUtc { get; set; }

    public DateTime? EstimatedNextResetUtc { get; set; }

    public string ResetCountdownText { get; set; } = "—";
}
