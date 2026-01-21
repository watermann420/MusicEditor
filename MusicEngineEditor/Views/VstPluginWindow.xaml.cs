using System;
using System.Windows;
using System.Windows.Media;
using MusicEngine.Core;

namespace MusicEngineEditor.Views;

public partial class VstPluginWindow : Window
{
    private readonly string _pluginName;
    private readonly string _variableName;
    private readonly IVstPlugin? _vstPlugin;
    private bool _isBypassed;
    private bool _keepRunning = true;

    public string PluginName => _pluginName;
    public string VariableName => _variableName;
    public bool KeepRunning => _keepRunning;
    public IVstPlugin? VstPlugin => _vstPlugin;

    /// <summary>
    /// Gets whether the plugin is currently bypassed.
    /// </summary>
    public bool IsBypassed => _isBypassed;

    /// <summary>
    /// Event raised when bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassStateChanged;

    public VstPluginWindow(string pluginName, string variableName, object? vstPlugin = null)
    {
        InitializeComponent();

        _pluginName = pluginName;
        _variableName = variableName;
        _vstPlugin = vstPlugin as IVstPlugin;

        Title = $"{pluginName} - VST Plugin";
        PluginNameText.Text = pluginName;
        VariableNameText.Text = variableName;

        // Determine plugin type based on name or actual type
        if (_vstPlugin != null)
        {
            PluginTypeText.Text = _vstPlugin.IsVst3 ? "(VST3)" : "(VST2)";

            // Sync bypass state with plugin
            _isBypassed = _vstPlugin.IsBypassed;
            UpdateBypassVisuals();

            // Update preset name if available
            UpdatePresetDisplay();

            // Subscribe to bypass changes from the plugin
            _vstPlugin.BypassChanged += OnPluginBypassChanged;
        }
        else if (pluginName.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase))
        {
            PluginTypeText.Text = "(VST3)";
        }
        else
        {
            PluginTypeText.Text = "(VST2)";
        }

        // Try to initialize plugin UI
        InitializePluginUI();
    }

    private void OnPluginBypassChanged(object? sender, bool isBypassed)
    {
        Dispatcher.Invoke(() =>
        {
            _isBypassed = isBypassed;
            UpdateBypassVisuals();
        });
    }

    private void InitializePluginUI()
    {
        if (_vstPlugin != null)
        {
            // Try to get the plugin editor window handle
            try
            {
                // This is where you would hook into the actual VST plugin UI
                // For now, show a placeholder
                PluginStatusText.Text = "Plugin loaded - UI available";
                PlaceholderPanel.Visibility = Visibility.Visible;
                VstHost.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                PluginStatusText.Text = $"UI not available: {ex.Message}";
            }
        }
        else
        {
            PluginStatusText.Text = "No plugin instance - using variable reference";
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Don't actually close - just hide the window
        // The plugin keeps running in the background
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_keepRunning)
        {
            // Instead of closing, just hide
            e.Cancel = true;
            Hide();
        }
        else
        {
            // Unsubscribe from events
            if (_vstPlugin != null)
            {
                _vstPlugin.BypassChanged -= OnPluginBypassChanged;
            }
            base.OnClosing(e);
        }
    }

    public void ForceClose()
    {
        _keepRunning = false;

        // Unsubscribe from events
        if (_vstPlugin != null)
        {
            _vstPlugin.BypassChanged -= OnPluginBypassChanged;
        }

        Close();
    }

    public void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    private void BypassButton_Click(object sender, RoutedEventArgs e)
    {
        _isBypassed = !_isBypassed;

        // Update the actual plugin bypass state
        if (_vstPlugin != null)
        {
            _vstPlugin.IsBypassed = _isBypassed;
        }

        UpdateBypassVisuals();

        // Raise event for external listeners
        BypassStateChanged?.Invoke(this, _isBypassed);
    }

    /// <summary>
    /// Sets the bypass state programmatically.
    /// </summary>
    public void SetBypass(bool bypassed)
    {
        if (_isBypassed != bypassed)
        {
            _isBypassed = bypassed;

            if (_vstPlugin != null)
            {
                _vstPlugin.IsBypassed = bypassed;
            }

            UpdateBypassVisuals();
            BypassStateChanged?.Invoke(this, _isBypassed);
        }
    }

    private void UpdateBypassVisuals()
    {
        // Update button text
        BypassButton.Content = _isBypassed ? "Enable" : "Bypass";

        // Update button styling
        if (_isBypassed)
        {
            // Active bypass state - orange/warning colors
            BypassButtonBorder.Background = (Brush)FindResource("BypassButtonActiveBrush");
            BypassButtonBorder.BorderBrush = (Brush)FindResource("BypassBorderBrush");
            BypassButton.Foreground = Brushes.White;

            // Show bypass overlay
            BypassOverlay.Visibility = Visibility.Visible;

            // Gray out the plugin host area
            PluginHostBorder.Opacity = 0.5;

            // Show bypass badge in header
            HeaderBypassBadge.Visibility = Visibility.Visible;

            // Update window title
            Title = $"{_pluginName} [BYPASSED] - VST Plugin";
        }
        else
        {
            // Normal state
            BypassButtonBorder.Background = Brushes.Transparent;
            BypassButtonBorder.BorderBrush = (Brush)FindResource("SubtleBorderBrush");
            BypassButton.Foreground = (Brush)FindResource("ForegroundBrush");

            // Hide bypass overlay
            BypassOverlay.Visibility = Visibility.Collapsed;

            // Restore plugin host opacity
            PluginHostBorder.Opacity = 1.0;

            // Hide bypass badge in header
            HeaderBypassBadge.Visibility = Visibility.Collapsed;

            // Restore window title
            UpdateWindowTitle();
        }
    }

    private void UpdateWindowTitle()
    {
        string presetPart = "";
        if (_vstPlugin != null && !string.IsNullOrEmpty(_vstPlugin.CurrentPresetName))
        {
            presetPart = $" - {_vstPlugin.CurrentPresetName}";
        }

        Title = $"{_pluginName}{presetPart} - VST Plugin";
    }

    private void UpdatePresetDisplay()
    {
        if (_vstPlugin != null && !string.IsNullOrEmpty(_vstPlugin.CurrentPresetName))
        {
            PresetNameText.Text = $"- {_vstPlugin.CurrentPresetName}";
        }
        else
        {
            PresetNameText.Text = "";
        }

        UpdateWindowTitle();
    }

    /// <summary>
    /// Refreshes the preset display from the plugin.
    /// </summary>
    public void RefreshPresetDisplay()
    {
        UpdatePresetDisplay();
    }

    private void PresetsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vstPlugin == null)
        {
            MessageBox.Show("No plugin instance available for preset management.",
                "Preset Browser", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Open the preset browser dialog
        var presetDialog = new Dialogs.VstPresetBrowserDialog(_vstPlugin, _pluginName)
        {
            Owner = this
        };

        if (presetDialog.ShowDialog() == true)
        {
            // Preset was loaded, update the display
            UpdatePresetDisplay();
        }
    }
}
