using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SubscriptionOverlayWidget.Models;

namespace SubscriptionOverlayWidget.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _initialSettings;
    private bool _isInitializing;

    public event EventHandler<AppSettings>? SettingsSaved;
    public event EventHandler? MoveOverlayRequested;

    public SettingsWindow(AppSettings settings)
    {
        _isInitializing = true;
        InitializeComponent();
        _initialSettings = settings;

        ApiKeyTextBox.Text = settings.ApiKey;
        RefreshIntervalTextBox.Text = settings.RefreshIntervalSeconds.ToString();
        BackgroundColorTextBox.Text = settings.OverlayBackgroundColor;
        TextColorTextBox.Text = settings.OverlayTextColor;
        BorderColorTextBox.Text = settings.OverlayBorderColor;
        LimitColorTextBox.Text = settings.LimitValueColor;

        SelectComboValue(TextFontSizeComboBox, settings.OverlayFontSize.ToString("0"));
        SelectComboValue(LimitFontSizeComboBox, settings.LimitValueFontSize.ToString("0"));

        ShowBrandingCheckBox.IsChecked = settings.ShowBranding;
        ShowSubscriptionCheckBox.IsChecked = settings.ShowSubscription;
        ShowResetTimerCheckBox.IsChecked = settings.ShowResetTimer;
        OverlayOpacitySlider.Value = Math.Max(20, Math.Min(100, settings.OverlayOpacity));

        UpdateColorButton(BackgroundColorButton, settings.OverlayBackgroundColor);
        UpdateColorButton(TextColorButton, settings.OverlayTextColor);
        UpdateColorButton(BorderColorButton, settings.OverlayBorderColor);
        UpdateColorButton(LimitColorButton, settings.LimitValueColor);
        UpdateSliderLabels();

        _isInitializing = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MoveOverlay_Click(object sender, RoutedEventArgs e)
    {
        MoveOverlayRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OverlaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSliderLabels();
        TriggerLiveApply(false);
    }

    private void LiveSettingsChanged(object sender, EventArgs e)
    {
        TriggerLiveApply(false);
    }

    private void BackgroundColorButton_Click(object sender, RoutedEventArgs e) => TogglePopup(BackgroundColorPopup);
    private void TextColorButton_Click(object sender, RoutedEventArgs e) => TogglePopup(TextColorPopup);
    private void BorderColorButton_Click(object sender, RoutedEventArgs e) => TogglePopup(BorderColorPopup);
    private void LimitColorButton_Click(object sender, RoutedEventArgs e) => TogglePopup(LimitColorPopup);

    private void PresetColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        var value = button.Tag?.ToString() ?? string.Empty;
        if (BackgroundColorPopup.IsOpen)
        {
            BackgroundColorTextBox.Text = value;
            BackgroundColorPopup.IsOpen = false;
        }
        else if (TextColorPopup.IsOpen)
        {
            TextColorTextBox.Text = value;
            TextColorPopup.IsOpen = false;
        }
        else if (BorderColorPopup.IsOpen)
        {
            BorderColorTextBox.Text = value;
            BorderColorPopup.IsOpen = false;
        }
        else if (LimitColorPopup.IsOpen)
        {
            LimitColorTextBox.Text = value;
            LimitColorPopup.IsOpen = false;
        }

        TriggerLiveApply(false);
    }

    private void ColorHexTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        if (IsValidColor(textBox.Text))
        {
            switch (textBox.Name)
            {
                case nameof(BackgroundColorTextBox):
                    UpdateColorButton(BackgroundColorButton, textBox.Text);
                    break;
                case nameof(TextColorTextBox):
                    UpdateColorButton(TextColorButton, textBox.Text);
                    break;
                case nameof(BorderColorTextBox):
                    UpdateColorButton(BorderColorButton, textBox.Text);
                    break;
                case nameof(LimitColorTextBox):
                    UpdateColorButton(LimitColorButton, textBox.Text);
                    break;
            }
        }

        TriggerLiveApply(false);
    }

    private bool TryBuildSettings(out AppSettings settings, bool showValidation)
    {
        settings = _initialSettings;

        if (!int.TryParse(RefreshIntervalTextBox.Text, out var interval))
        {
            if (showValidation)
            {
                System.Windows.MessageBox.Show(this, "Refresh interval must be a number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        if (TextFontSizeComboBox.SelectedItem is not ComboBoxItem textFontItem || !double.TryParse(textFontItem.Content?.ToString(), out var textFontSize))
        {
            if (showValidation)
            {
                System.Windows.MessageBox.Show(this, "Text font size is invalid.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        if (LimitFontSizeComboBox.SelectedItem is not ComboBoxItem limitFontItem || !double.TryParse(limitFontItem.Content?.ToString(), out var limitFontSize))
        {
            if (showValidation)
            {
                System.Windows.MessageBox.Show(this, "Limit font size is invalid.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        if (!IsValidColor(BackgroundColorTextBox.Text) || !IsValidColor(TextColorTextBox.Text) || !IsValidColor(BorderColorTextBox.Text) || !IsValidColor(LimitColorTextBox.Text))
        {
            if (showValidation)
            {
                System.Windows.MessageBox.Show(this, "Color value is invalid.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        settings = new AppSettings
        {
            ApiKey = ApiKeyTextBox.Text.Trim(),
            ApiKeyEncrypted = _initialSettings.ApiKeyEncrypted,
            RefreshIntervalSeconds = Math.Max(15, interval),
            StartOverlayVisible = true,
            OverlayBackgroundColor = BackgroundColorTextBox.Text.Trim(),
            OverlayTextColor = TextColorTextBox.Text.Trim(),
            OverlayBorderColor = BorderColorTextBox.Text.Trim(),
            LimitValueColor = LimitColorTextBox.Text.Trim(),
            ShowBranding = ShowBrandingCheckBox.IsChecked == true,
            ShowSubscription = ShowSubscriptionCheckBox.IsChecked == true,
            ShowResetTimer = ShowResetTimerCheckBox.IsChecked == true,
            OverlayFontSize = Math.Max(10, textFontSize),
            LimitValueFontSize = Math.Max(18, limitFontSize),
            OverlayOpacity = Math.Max(20, OverlayOpacitySlider.Value),
            OverlayLeft = _initialSettings.OverlayLeft,
            OverlayTop = _initialSettings.OverlayTop,
            LastKnownSubscriptionType = _initialSettings.LastKnownSubscriptionType,
            LastKnownRemainingLimit = _initialSettings.LastKnownRemainingLimit,
            LastObservedAtUtc = _initialSettings.LastObservedAtUtc,
            LastResetAtUtc = _initialSettings.LastResetAtUtc
        };

        return true;
    }

    private void TriggerLiveApply(bool showValidation)
    {
        if (_isInitializing)
        {
            return;
        }

        if (!TryBuildSettings(out var settings, showValidation))
        {
            return;
        }

        SettingsSaved?.Invoke(this, settings);
    }

    private void UpdateSliderLabels()
    {
        if (OverlayOpacityValueTextBlock is not null)
        {
            OverlayOpacityValueTextBlock.Text = $"{OverlayOpacitySlider.Value:0}%";
        }
    }

    private static void TogglePopup(Popup popup)
    {
        popup.IsOpen = !popup.IsOpen;
    }

    private void UpdateColorButton(System.Windows.Controls.Button button, string colorValue)
    {
        if (!IsValidColor(colorValue))
        {
            return;
        }

        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorValue);
        button.Background = new SolidColorBrush(color);
        button.BorderBrush = new SolidColorBrush(color);
    }

    private static bool IsValidColor(string value)
    {
        try
        {
            _ = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SelectComboValue(System.Windows.Controls.ComboBox comboBox, string value)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => string.Equals(i.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase));
    }
}
