using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SubscriptionOverlayWidget.Models;

namespace SubscriptionOverlayWidget.Views;

public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;
    private bool _isMoveModeEnabled;
    private string _lastStatusText = "Инициализация...";

    public event EventHandler<OverlayPositionChangedEventArgs>? PositionChanged;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            PositionTopRight();
            ApplyWindowIcon();
            ApplyClickThrough();
        };
        SizeChanged += (_, _) =>
        {
            if (!_isMoveModeEnabled)
            {
                PositionTopRight();
            }
        };
        LocationChanged += (_, _) => RaisePositionChanged();
    }

    public void SetStatus(string text)
    {
        _lastStatusText = text;
    }

    public void SetData(SubscriptionLimitInfo info)
    {
        LimitTextBlock.Text = FormatLimit(info.RemainingLimitValue, info.RemainingLimit);
        SubscriptionLevelTextBlock.Text = info.SubscriptionType;
        ResetTextBlock.Text = string.IsNullOrWhiteSpace(info.ResetCountdownText) ? "Сброс лимитов: —" : info.ResetCountdownText;
        SetStatus(info.StatusText);

        if (!info.IsSuccess)
        {
            LimitTextBlock.Text = "—";
        }
    }

    public void SetLiveCountdown(string text)
    {
        ResetTextBlock.Text = text;
    }

    public void ApplySettings(AppSettings settings)
    {
        Opacity = Math.Max(0.2, Math.Min(1.0, settings.OverlayOpacity / 100.0));
        LimitTextBlock.FontSize = Math.Max(12, settings.LimitValueFontSize);
        SubscriptionLevelTextBlock.FontSize = Math.Max(10, settings.OverlayFontSize);
        BrandTextBlock.FontSize = Math.Max(10, settings.OverlayFontSize);
        ResetTextBlock.FontSize = Math.Max(10, settings.OverlayFontSize);

        OverlayBorder.Background = CreateBrush(settings.OverlayBackgroundColor, "#121C27");
        OverlayBorder.BorderBrush = CreateBrush(settings.OverlayBorderColor, "#253246");

        var textBrush = CreateBrush(settings.OverlayTextColor, "#E6EDF3");
        var limitBrush = CreateBrush(settings.LimitValueColor, "#E6EDF3");

        BrandTextBlock.Foreground = System.Windows.Media.Brushes.White;
        SubscriptionLevelTextBlock.Foreground = textBrush;
        ResetTextBlock.Foreground = textBrush;
        LimitTextBlock.Foreground = limitBrush;

        BrandingPanel.Visibility = settings.ShowBranding ? Visibility.Visible : Visibility.Collapsed;
        SubscriptionPanel.Visibility = settings.ShowSubscription ? Visibility.Visible : Visibility.Collapsed;
        ResetPanel.Visibility = settings.ShowResetTimer ? Visibility.Visible : Visibility.Collapsed;

        if (settings.OverlayLeft >= 0 && settings.OverlayTop >= 0)
        {
            Left = settings.OverlayLeft;
            Top = settings.OverlayTop;
        }
        else
        {
            PositionTopRight();
        }
    }

    public void EnableMoveMode()
    {
        _isMoveModeEnabled = true;
        Topmost = false;
        Activate();
        _lastStatusText = "Режим перемещения: перетащите окно мышкой.";
        RemoveClickThrough();
    }

    public void DisableMoveMode()
    {
        _isMoveModeEnabled = false;
        Topmost = true;
        _ = _lastStatusText;
        ApplyClickThrough();
    }

    public void ShowOverlay()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (Left < 0 || Top < 0)
        {
            PositionTopRight();
        }

        Activate();
    }

    private void PositionTopRight()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Max(0, area.Right - Width - 16);
        Top = Math.Max(0, area.Top + 16);
    }

    private void OverlayBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isMoveModeEnabled)
        {
            return;
        }

        try
        {
            DragMove();
        }
        finally
        {
            DisableMoveMode();
        }
    }

    private void RaisePositionChanged()
    {
        PositionChanged?.Invoke(this, new OverlayPositionChangedEventArgs(Left, Top));
    }

    private void ApplyWindowIcon()
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/logo.png", UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            LogoImage.Source = image;
        }
        catch
        {
        }
    }

    private void ApplyClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var extendedStyle = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, extendedStyle | WsExTransparent | WsExNoActivate);
    }

    private void RemoveClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var extendedStyle = GetWindowLong(hwnd, GwlExStyle);
        extendedStyle &= ~WsExTransparent;
        extendedStyle &= ~WsExNoActivate;
        SetWindowLong(hwnd, GwlExStyle, extendedStyle);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private static SolidColorBrush CreateBrush(string? colorValue, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(colorValue) ? fallback : colorValue;

        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(raw);
            return new SolidColorBrush(color);
        }
        catch
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fallback);
            return new SolidColorBrush(color);
        }
    }

    private static string FormatLimit(long? numericValue, string rawValue)
    {
        if (numericValue.HasValue)
        {
            var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            format.NumberGroupSeparator = " ";
            return numericValue.Value.ToString("N0", format);
        }

        return rawValue;
    }
}

public sealed class OverlayPositionChangedEventArgs : EventArgs
{
    public OverlayPositionChangedEventArgs(double left, double top)
    {
        Left = left;
        Top = top;
    }

    public double Left { get; }

    public double Top { get; }
}
