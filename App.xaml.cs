using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using SubscriptionOverlayWidget.Models;
using SubscriptionOverlayWidget.Services;
using SubscriptionOverlayWidget.Views;

namespace SubscriptionOverlayWidget;

public partial class App : System.Windows.Application
{
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
        var iconPath = Path.Combine(AppContext.BaseDirectory, "favicon.ico");
        var trayIcon = File.Exists(iconPath)
            ? new System.Drawing.Icon(iconPath)
            : System.Drawing.SystemIcons.Information;

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = trayIcon,
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
}
