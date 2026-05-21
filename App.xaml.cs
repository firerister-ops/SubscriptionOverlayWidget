using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using SubscriptionOverlayWidget.Models;
using SubscriptionOverlayWidget.Services;
using SubscriptionOverlayWidget.Views;

namespace SubscriptionOverlayWidget;

public partial class App : System.Windows.Application
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HotkeyId = 9000;
    private const uint ModAlt = 0x0001;
    private const uint ModShift = 0x0004;
    private const uint ModCtrl = 0x0002;
    private const uint ModNone = 0x0000;

    private readonly DebugLogService _debugLogService = new();
    private readonly SettingsService _settingsService = new();
    private readonly SubscriptionService _subscriptionService = new();
    private readonly SubscriptionResetEstimator _resetEstimator = new();
    private OverlayWindow? _overlayWindow;
    private SettingsWindow? _settingsWindow;
    private Forms.NotifyIcon? _notifyIcon;
    private DispatcherTimer? _timer;
    private DispatcherTimer? _countdownTimer;
    private AppSettings _settings = new();
    private HwndSource? _hwndSource;
    private bool _hotkeyRegistered;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += async (_, args) =>
        {
            await _debugLogService.WriteExceptionAsync("DispatcherUnhandledException", args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += async (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                await _debugLogService.WriteExceptionAsync("AppDomainUnhandledException", exception);
            }
        };

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _settings = await _settingsService.LoadAsync();
        await _debugLogService.WriteAsync("Application startup");

        _overlayWindow = new OverlayWindow();
        _overlayWindow.ApplySettings(_settings);
        _overlayWindow.PositionChanged += async (_, position) =>
        {
            _settings.OverlayLeft = position.Left;
            _settings.OverlayTop = position.Top;
            await _settingsService.SaveAsync(_settings);
        };
        _overlayWindow.Loaded += (_, _) => RegisterGlobalHotkey();
        CreateTrayIcon();
        ConfigureTimer();
        ConfigureCountdownTimer();

        ShowOverlay();

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            ShowSettings();
            _overlayWindow.SetData(new SubscriptionLimitInfo
            {
                SubscriptionType = _settings.LastKnownSubscriptionType,
                StatusText = "Добавьте API key в настройках.",
                ResetCountdownText = _resetEstimator.GetLiveCountdown(_settings)
            });
        }
        else
        {
            await RefreshNowAsync();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        UnregisterGlobalHotkey();
        _timer?.Stop();
        _countdownTimer?.Stop();

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _overlayWindow?.Close();
        _settingsWindow?.Close();
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        System.Drawing.Icon? trayIcon = null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("pack://application:,,,/logo.png", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            var tempPath = Path.Combine(Path.GetTempPath(), "sow_tray_icon.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var fs = File.Create(tempPath))
            {
                encoder.Save(fs);
            }

            using var bmp = new System.Drawing.Bitmap(tempPath);
            var handle = bmp.GetHicon();
            trayIcon = System.Drawing.Icon.FromHandle(handle);
        }
        catch
        {
        }

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = trayIcon ?? System.Drawing.SystemIcons.Information,
            Text = "Subscription Overlay Widget",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };

        _notifyIcon.DoubleClick += (_, _) => ToggleOverlay();
        _notifyIcon.ContextMenuStrip.Items.Add("Show Overlay", null, (_, _) => ShowOverlay());
        _notifyIcon.ContextMenuStrip.Items.Add("Hide Overlay", null, (_, _) => HideOverlay());
        _notifyIcon.ContextMenuStrip.Items.Add("Settings", null, (_, _) => ShowSettings());
        _notifyIcon.ContextMenuStrip.Items.Add("Refresh Now", null, async (_, _) => await RefreshNowAsync());
        _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Shutdown());
    }

    private void ConfigureTimer()
    {
        _timer = new DispatcherTimer();
        _timer.Tick += async (_, _) => await RefreshNowAsync();
        ApplyTimerInterval();
        _timer.Start();
    }

    private void ConfigureCountdownTimer()
    {
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += (_, _) =>
        {
            if (_overlayWindow is null)
            {
                return;
            }

            _overlayWindow.SetLiveCountdown(_resetEstimator.GetLiveCountdown(_settings));
        };
        _countdownTimer.Start();
    }

    private void ApplyTimerInterval()
    {
        if (_timer is null)
        {
            return;
        }

        var seconds = Math.Max(15, _settings.RefreshIntervalSeconds);
        _timer.Interval = TimeSpan.FromSeconds(seconds);
    }

    private void ToggleOverlay()
    {
        if (_overlayWindow is null)
        {
            return;
        }

        if (_overlayWindow.IsVisible)
        {
            HideOverlay();
        }
        else
        {
            ShowOverlay();
        }
    }

    private void ShowOverlay()
    {
        if (_overlayWindow is null)
        {
            return;
        }

        _overlayWindow.ShowOverlay();
        _settings.StartOverlayVisible = true;
        _ = _settingsService.SaveAsync(_settings);
    }

    private void HideOverlay()
    {
        _overlayWindow?.Hide();
        _settings.StartOverlayVisible = false;
        _ = _settingsService.SaveAsync(_settings);
    }

    private void ShowSettings()
    {
        _ = _debugLogService.WriteAsync("ShowSettings called");

        if (_settingsWindow is null || !_settingsWindow.IsLoaded)
        {
            _ = _debugLogService.WriteAsync("Creating SettingsWindow instance");
            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.MoveOverlayRequested += (_, _) =>
            {
                _ = _debugLogService.WriteAsync("MoveOverlayRequested triggered");
                ShowOverlay();
                _overlayWindow?.EnableMoveMode();
            };
            _settingsWindow.SettingsSaved += async (_, newSettings) =>
            {
                await _debugLogService.WriteAsync("SettingsSaved triggered");
                _settings = newSettings;
                await _settingsService.SaveAsync(_settings);
                ApplyTimerInterval();
                _overlayWindow?.ApplySettings(_settings);
                RegisterGlobalHotkey();

                ShowOverlay();
                await RefreshNowAsync();
            };
            _settingsWindow.Closed += async (_, _) =>
            {
                await _debugLogService.WriteAsync("SettingsWindow closed");
                _settingsWindow = null;
            };
            _settingsWindow.Show();
            _ = _debugLogService.WriteAsync("SettingsWindow shown");
            return;
        }

        _ = _debugLogService.WriteAsync("SettingsWindow activate existing instance");
        _settingsWindow.Activate();
    }

    private async Task RefreshNowAsync()
    {
        if (_overlayWindow is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _overlayWindow.SetData(new SubscriptionLimitInfo
            {
                SubscriptionType = _settings.LastKnownSubscriptionType,
                StatusText = "Нет API key. Откройте Settings.",
                ResetCountdownText = _resetEstimator.GetLiveCountdown(_settings)
            });
            return;
        }

        _overlayWindow.SetData(new SubscriptionLimitInfo
        {
            StatusText = "Загрузка лимитов..."
        });
        var result = await _subscriptionService.FetchSummaryAsync(_settings.ApiKey);
        _resetEstimator.Apply(_settings, result);
        await _settingsService.SaveAsync(_settings);
        _overlayWindow.SetData(result);
    }

    private void RegisterGlobalHotkey()
    {
        UnregisterGlobalHotkey();

        if (_overlayWindow is null)
        {
            return;
        }

        var key = _settings.HotkeyKey?.Trim() ?? "";
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        var vk = (uint)char.ToUpper(key[0]);
        if (vk < 0x41 || vk > 0x5A)
        {
            return;
        }

        var mod = _settings.HotkeyModifier?.Trim().ToLowerInvariant() switch
        {
            "alt" => ModAlt,
            "shift" => ModShift,
            "ctrl" => ModCtrl,
            _ => ModNone
        };

        var hwnd = new WindowInteropHelper(_overlayWindow).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (_hwndSource is null)
        {
            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        if (RegisterHotKey(hwnd, HotkeyId, mod, vk))
        {
            _hotkeyRegistered = true;
        }
    }

    private void UnregisterGlobalHotkey()
    {
        if (!_hotkeyRegistered)
        {
            return;
        }

        var hwnd = _overlayWindow is not null
            ? new WindowInteropHelper(_overlayWindow).Handle
            : IntPtr.Zero;

        if (hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(hwnd, HotkeyId);
        }

        _hotkeyRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmHotkey = 0x0312;
        if (msg == wmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleOverlay();
        }

        return IntPtr.Zero;
    }
}
