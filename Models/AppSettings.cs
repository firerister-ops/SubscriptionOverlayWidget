using System.Text.Json.Serialization;

namespace SubscriptionOverlayWidget.Models;

public sealed class AppSettings
{
    [JsonIgnore]
    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyEncrypted { get; set; } = string.Empty;

    public int RefreshIntervalSeconds { get; set; } = 60;

    public bool StartOverlayVisible { get; set; } = true;

    public string OverlayBackgroundColor { get; set; } = "#121C27";

    public string OverlayTextColor { get; set; } = "#E6EDF3";

    public string OverlayBorderColor { get; set; } = "#253246";

    public string LimitValueColor { get; set; } = "#E6EDF3";

    public bool ShowBranding { get; set; } = true;

    public bool ShowSubscription { get; set; } = true;

    public bool ShowResetTimer { get; set; } = true;

    public double OverlayFontSize { get; set; } = 12;

    public double LimitValueFontSize { get; set; } = 30;

    public double OverlayOpacity { get; set; } = 88;

    public string HotkeyModifier { get; set; } = "None";

    public string HotkeyKey { get; set; } = "";

    public double OverlayLeft { get; set; } = -1;

    public double OverlayTop { get; set; } = -1;

    public string LastKnownSubscriptionType { get; set; } = string.Empty;

    public long LastKnownRemainingLimit { get; set; } = -1;

    public DateTime? LastObservedAtUtc { get; set; }

    public DateTime? LastResetAtUtc { get; set; }
}
