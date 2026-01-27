// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Performance meter visualization.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A compact horizontal performance meter showing CPU, RAM, buffer, disk, and ASIO status.
/// Click to open a detailed performance dialog.
/// </summary>
public partial class PerformanceMeter : UserControl
{
    #region Constants

    private const double LowThreshold = 50.0;   // 0-50% = Green
    private const double MediumThreshold = 80.0; // 50-80% = Yellow
    // 80-100% = Red

    private static readonly Color LowColor = Color.FromRgb(0x00, 0xFF, 0x88);    // Green
    private static readonly Color MediumColor = Color.FromRgb(0xFF, 0xB8, 0x00); // Yellow
    private static readonly Color HighColor = Color.FromRgb(0xFF, 0x47, 0x57);   // Red

    // RAM Colors
    private static readonly Color RamLowColor = Color.FromRgb(0x00, 0xD9, 0xFF);    // Cyan
    private static readonly Color RamMediumColor = Color.FromRgb(0xFF, 0xB8, 0x00); // Yellow
    private static readonly Color RamHighColor = Color.FromRgb(0xFF, 0x47, 0x57);   // Red

    // Buffer Colors
    private static readonly Color BufferOkColor = Color.FromRgb(0x00, 0xFF, 0x88);      // Green
    private static readonly Color BufferWarningColor = Color.FromRgb(0xFF, 0xB8, 0x00); // Yellow
    private static readonly Color BufferCriticalColor = Color.FromRgb(0xFF, 0x47, 0x57);// Red

    // Disk Activity Color
    private static readonly Color DiskActiveColor = Color.FromRgb(0x58, 0x9D, 0xF6);    // Blue

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty CpuUsageProperty =
        DependencyProperty.Register(nameof(CpuUsage), typeof(double), typeof(PerformanceMeter),
            new PropertyMetadata(0.0, OnCpuUsageChanged));

    public static readonly DependencyProperty MemoryUsageMBProperty =
        DependencyProperty.Register(nameof(MemoryUsageMB), typeof(double), typeof(PerformanceMeter),
            new PropertyMetadata(0.0, OnMemoryUsageChanged));

    public static readonly DependencyProperty DropoutCountProperty =
        DependencyProperty.Register(nameof(DropoutCount), typeof(int), typeof(PerformanceMeter),
            new PropertyMetadata(0, OnDropoutCountChanged));

    public static readonly DependencyProperty PeakCpuUsageProperty =
        DependencyProperty.Register(nameof(PeakCpuUsage), typeof(double), typeof(PerformanceMeter),
            new PropertyMetadata(0.0, OnTooltipPropertyChanged));

    public static readonly DependencyProperty AverageCpuUsageProperty =
        DependencyProperty.Register(nameof(AverageCpuUsage), typeof(double), typeof(PerformanceMeter),
            new PropertyMetadata(0.0, OnTooltipPropertyChanged));

    public static readonly DependencyProperty PeakMemoryUsageMBProperty =
        DependencyProperty.Register(nameof(PeakMemoryUsageMB), typeof(double), typeof(PerformanceMeter),
            new PropertyMetadata(0.0, OnTooltipPropertyChanged));

    public static readonly DependencyProperty LatencyMsProperty =
        DependencyProperty.Register(nameof(LatencyMs), typeof(double), typeof(PerformanceMeter),
            new PropertyMetadata(0.0, OnTooltipPropertyChanged));

    public static readonly DependencyProperty BufferSizeProperty =
        DependencyProperty.Register(nameof(BufferSize), typeof(int), typeof(PerformanceMeter),
            new PropertyMetadata(512, OnTooltipPropertyChanged));

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(PerformanceMeter),
            new PropertyMetadata(44100, OnTooltipPropertyChanged));

    public static readonly DependencyProperty RamUsagePercentProperty =
        DependencyProperty.Register(nameof(RamUsagePercent), typeof(double), typeof(PerformanceMeter),
            new PropertyMetadata(0.0, OnRamUsageChanged));

    public static readonly DependencyProperty BufferFillPercentProperty =
        DependencyProperty.Register(nameof(BufferFillPercent), typeof(double), typeof(PerformanceMeter),
            new PropertyMetadata(100.0, OnBufferFillChanged));

    public static readonly DependencyProperty DiskReadMBsProperty =
        DependencyProperty.Register(nameof(DiskReadMBs), typeof(double), typeof(PerformanceMeter),
            new PropertyMetadata(0.0, OnDiskReadChanged));

    public static readonly DependencyProperty AsioBufferStatusProperty =
        DependencyProperty.Register(nameof(AsioBufferStatus), typeof(string), typeof(PerformanceMeter),
            new PropertyMetadata("OK", OnAsioStatusChanged));

    /// <summary>
    /// Current CPU usage (0-100%).
    /// </summary>
    public double CpuUsage
    {
        get => (double)GetValue(CpuUsageProperty);
        set => SetValue(CpuUsageProperty, value);
    }

    /// <summary>
    /// Current memory usage in MB.
    /// </summary>
    public double MemoryUsageMB
    {
        get => (double)GetValue(MemoryUsageMBProperty);
        set => SetValue(MemoryUsageMBProperty, value);
    }

    /// <summary>
    /// Number of buffer underruns/dropouts.
    /// </summary>
    public int DropoutCount
    {
        get => (int)GetValue(DropoutCountProperty);
        set => SetValue(DropoutCountProperty, value);
    }

    /// <summary>
    /// Peak CPU usage.
    /// </summary>
    public double PeakCpuUsage
    {
        get => (double)GetValue(PeakCpuUsageProperty);
        set => SetValue(PeakCpuUsageProperty, value);
    }

    /// <summary>
    /// Average CPU usage.
    /// </summary>
    public double AverageCpuUsage
    {
        get => (double)GetValue(AverageCpuUsageProperty);
        set => SetValue(AverageCpuUsageProperty, value);
    }

    /// <summary>
    /// Peak memory usage in MB.
    /// </summary>
    public double PeakMemoryUsageMB
    {
        get => (double)GetValue(PeakMemoryUsageMBProperty);
        set => SetValue(PeakMemoryUsageMBProperty, value);
    }

    /// <summary>
    /// Audio latency in milliseconds.
    /// </summary>
    public double LatencyMs
    {
        get => (double)GetValue(LatencyMsProperty);
        set => SetValue(LatencyMsProperty, value);
    }

    /// <summary>
    /// Audio buffer size in samples.
    /// </summary>
    public int BufferSize
    {
        get => (int)GetValue(BufferSizeProperty);
        set => SetValue(BufferSizeProperty, value);
    }

    /// <summary>
    /// Audio sample rate in Hz.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>
    /// Current RAM usage percentage (0-100%).
    /// </summary>
    public double RamUsagePercent
    {
        get => (double)GetValue(RamUsagePercentProperty);
        set => SetValue(RamUsagePercentProperty, value);
    }

    /// <summary>
    /// Current audio buffer fill level percentage (0-100%).
    /// Higher is better - low values indicate potential dropouts.
    /// </summary>
    public double BufferFillPercent
    {
        get => (double)GetValue(BufferFillPercentProperty);
        set => SetValue(BufferFillPercentProperty, value);
    }

    /// <summary>
    /// Current disk read speed in MB/s for streaming.
    /// </summary>
    public double DiskReadMBs
    {
        get => (double)GetValue(DiskReadMBsProperty);
        set => SetValue(DiskReadMBsProperty, value);
    }

    /// <summary>
    /// Current ASIO buffer status: "OK", "Warning", or "Dropout".
    /// </summary>
    public string AsioBufferStatus
    {
        get => (string)GetValue(AsioBufferStatusProperty);
        set => SetValue(AsioBufferStatusProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the user clicks on the meter to open the detailed dialog.
    /// </summary>
    public event EventHandler? OpenDetailsRequested;

    #endregion

    #region Private Fields

    private PerformanceViewModel? _viewModel;

    #endregion

    #region Constructor

    public PerformanceMeter()
    {
        InitializeComponent();
        UpdateTooltip();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Connects this meter to a PerformanceViewModel for automatic updates.
    /// </summary>
    public void ConnectToViewModel(PerformanceViewModel viewModel)
    {
        // Disconnect from previous if any
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateFromViewModel();
        }
    }

    /// <summary>
    /// Resets the dropout counter.
    /// </summary>
    public void ResetDropoutCount()
    {
        DropoutCount = 0;
        _viewModel?.ResetStatsCommand.Execute(null);
    }

    #endregion

    #region Property Change Handlers

    private static void OnCpuUsageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceMeter meter)
        {
            meter.UpdateCpuDisplay();
            meter.UpdateTooltip();
        }
    }

    private static void OnMemoryUsageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceMeter meter)
        {
            meter.UpdateMemoryDisplay();
            meter.UpdateTooltip();
        }
    }

    private static void OnDropoutCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceMeter meter)
        {
            meter.UpdateDropoutDisplay();
            meter.UpdateTooltip();
        }
    }

    private static void OnTooltipPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceMeter meter)
        {
            meter.UpdateTooltip();
        }
    }

    private static void OnRamUsageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceMeter meter)
        {
            meter.UpdateRamDisplay();
            meter.UpdateTooltip();
        }
    }

    private static void OnBufferFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceMeter meter)
        {
            meter.UpdateBufferDisplay();
            meter.UpdateTooltip();
        }
    }

    private static void OnDiskReadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceMeter meter)
        {
            meter.UpdateDiskDisplay();
            meter.UpdateTooltip();
        }
    }

    private static void OnAsioStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceMeter meter)
        {
            meter.UpdateAsioStatusDisplay();
            meter.UpdateTooltip();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(PerformanceViewModel.CpuUsage):
                    CpuUsage = _viewModel?.CpuUsage ?? 0;
                    break;
                case nameof(PerformanceViewModel.MemoryUsageMB):
                    MemoryUsageMB = _viewModel?.MemoryUsageMB ?? 0;
                    break;
                case nameof(PerformanceViewModel.DropoutCount):
                    DropoutCount = _viewModel?.DropoutCount ?? 0;
                    break;
                case nameof(PerformanceViewModel.PeakCpuUsage):
                    PeakCpuUsage = _viewModel?.PeakCpuUsage ?? 0;
                    break;
                case nameof(PerformanceViewModel.AverageCpuUsage):
                    AverageCpuUsage = _viewModel?.AverageCpuUsage ?? 0;
                    break;
                case nameof(PerformanceViewModel.PeakMemoryUsageMB):
                    PeakMemoryUsageMB = _viewModel?.PeakMemoryUsageMB ?? 0;
                    break;
                case nameof(PerformanceViewModel.LatencyMs):
                    LatencyMs = _viewModel?.LatencyMs ?? 0;
                    break;
                case nameof(PerformanceViewModel.BufferSize):
                    BufferSize = _viewModel?.BufferSize ?? 512;
                    break;
                case nameof(PerformanceViewModel.SampleRate):
                    SampleRate = _viewModel?.SampleRate ?? 44100;
                    break;
            }
        });
    }

    #endregion

    #region UI Update Methods

    private void UpdateFromViewModel()
    {
        if (_viewModel == null) return;

        CpuUsage = _viewModel.CpuUsage;
        MemoryUsageMB = _viewModel.MemoryUsageMB;
        DropoutCount = _viewModel.DropoutCount;
        PeakCpuUsage = _viewModel.PeakCpuUsage;
        AverageCpuUsage = _viewModel.AverageCpuUsage;
        PeakMemoryUsageMB = _viewModel.PeakMemoryUsageMB;
        LatencyMs = _viewModel.LatencyMs;
        BufferSize = _viewModel.BufferSize;
        SampleRate = _viewModel.SampleRate;
    }

    private void UpdateCpuDisplay()
    {
        // Update percentage text
        CpuPercentText.Text = $"{CpuUsage:F0}%";

        // Update bar width (max width is container width minus margins)
        var maxWidth = 56.0; // 60 - 4 for margins
        var barWidth = Math.Max(0, Math.Min(maxWidth, maxWidth * CpuUsage / 100.0));
        CpuBar.Width = barWidth;

        // Update bar color based on CPU level
        var color = GetCpuColor(CpuUsage);
        CpuBarBrush.Color = color;

        // Update text color for high CPU
        if (CpuUsage >= MediumThreshold)
        {
            CpuPercentText.Foreground = new SolidColorBrush(color);
        }
        else
        {
            CpuPercentText.Foreground = FindResource("TextBrightBrush") as Brush;
        }

        // Add glow effect for very high CPU
        if (CpuUsage >= MediumThreshold)
        {
            CpuBar.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = color,
                BlurRadius = 6,
                ShadowDepth = 0,
                Opacity = 0.6
            };
        }
        else
        {
            CpuBar.Effect = null;
        }
    }

    private void UpdateMemoryDisplay()
    {
        // Memory is now displayed as RAM percentage instead of MB
        // The MemoryUsageMB property is still tracked for tooltip display
    }

    private void UpdateDropoutDisplay()
    {
        if (DropoutCount > 0)
        {
            DropoutBadge.Visibility = Visibility.Visible;
            DropoutCountText.Text = DropoutCount.ToString();
        }
        else
        {
            DropoutBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateRamDisplay()
    {
        // Update percentage text
        RamPercentText.Text = $"{RamUsagePercent:F0}%";

        // Update bar width (max width is container width minus margins)
        var maxWidth = 46.0; // 50 - 4 for margins
        var barWidth = Math.Max(0, Math.Min(maxWidth, maxWidth * RamUsagePercent / 100.0));
        RamBar.Width = barWidth;

        // Update bar color based on RAM level
        var color = GetRamColor(RamUsagePercent);
        RamBarBrush.Color = color;

        // Update text color for high RAM usage
        if (RamUsagePercent >= MediumThreshold)
        {
            RamPercentText.Foreground = new SolidColorBrush(color);
        }
        else
        {
            RamPercentText.Foreground = FindResource("TextBrightBrush") as Brush;
        }

        // Add glow effect for very high RAM usage
        if (RamUsagePercent >= MediumThreshold)
        {
            RamBar.Effect = new DropShadowEffect
            {
                Color = color,
                BlurRadius = 6,
                ShadowDepth = 0,
                Opacity = 0.6
            };
        }
        else
        {
            RamBar.Effect = null;
        }
    }

    private void UpdateBufferDisplay()
    {
        // Update bar width (max width is container width minus margins)
        var maxWidth = 26.0; // 30 - 4 for margins
        var barWidth = Math.Max(0, Math.Min(maxWidth, maxWidth * BufferFillPercent / 100.0));
        BufferBar.Width = barWidth;

        // Update bar color based on buffer level (inverted - low is bad)
        var color = GetBufferColor(BufferFillPercent);
        BufferBarBrush.Color = color;

        // Add glow effect for critical buffer levels
        if (BufferFillPercent < 30)
        {
            BufferBar.Effect = new DropShadowEffect
            {
                Color = color,
                BlurRadius = 6,
                ShadowDepth = 0,
                Opacity = 0.6
            };
        }
        else
        {
            BufferBar.Effect = null;
        }
    }

    private void UpdateDiskDisplay()
    {
        // Update disk read text
        if (DiskReadMBs >= 1.0)
        {
            DiskReadText.Text = $"{DiskReadMBs:F1}";
        }
        else if (DiskReadMBs > 0)
        {
            DiskReadText.Text = $"{DiskReadMBs * 1024:F0}K";
        }
        else
        {
            DiskReadText.Text = "0";
        }

        // Update indicator color and glow based on activity
        if (DiskReadMBs > 0)
        {
            DiskIndicator.Background = new SolidColorBrush(DiskActiveColor);
            DiskGlow.BlurRadius = Math.Min(8, 2 + DiskReadMBs * 2);
            DiskGlow.Opacity = Math.Min(0.8, 0.3 + DiskReadMBs * 0.1);
        }
        else
        {
            DiskIndicator.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            DiskGlow.BlurRadius = 0;
            DiskGlow.Opacity = 0;
        }
    }

    private void UpdateAsioStatusDisplay()
    {
        Color statusColor;
        double glowOpacity;

        switch (AsioBufferStatus?.ToUpperInvariant())
        {
            case "WARNING":
                statusColor = BufferWarningColor;
                glowOpacity = 0.6;
                break;
            case "DROPOUT":
                statusColor = BufferCriticalColor;
                glowOpacity = 0.8;
                break;
            case "OK":
            default:
                statusColor = BufferOkColor;
                glowOpacity = 0.5;
                break;
        }

        AsioStatusIndicator.Background = new SolidColorBrush(statusColor);
        AsioGlow.Color = statusColor;
        AsioGlow.Opacity = glowOpacity;

        // Add pulsing effect for dropout status
        if (AsioBufferStatus?.ToUpperInvariant() == "DROPOUT")
        {
            AsioGlow.BlurRadius = 8;
        }
        else
        {
            AsioGlow.BlurRadius = 4;
        }
    }

    private void UpdateTooltip()
    {
        var asioStatusDisplay = AsioBufferStatus ?? "OK";
        var diskReadDisplay = DiskReadMBs >= 1.0 ? $"{DiskReadMBs:F1} MB/s" : $"{DiskReadMBs * 1024:F0} KB/s";

        var tooltipText = $"CPU Usage: {CpuUsage:F1}%\n" +
                          $"  Peak: {PeakCpuUsage:F1}%\n" +
                          $"  Average: {AverageCpuUsage:F1}%\n" +
                          $"\nMemory: {MemoryUsageMB:F1} MB\n" +
                          $"  Peak: {PeakMemoryUsageMB:F1} MB\n" +
                          $"\nRAM Usage: {RamUsagePercent:F1}%\n" +
                          $"\nAudio Buffer:\n" +
                          $"  Fill Level: {BufferFillPercent:F0}%\n" +
                          $"  Buffer Underruns: {DropoutCount}\n" +
                          $"  ASIO Status: {asioStatusDisplay}\n" +
                          $"\nDisk Activity:\n" +
                          $"  Read Speed: {diskReadDisplay}\n" +
                          $"\nAudio Settings:\n" +
                          $"  Latency: {LatencyMs:F1} ms\n" +
                          $"  Buffer: {BufferSize} samples\n" +
                          $"  Sample Rate: {SampleRate} Hz\n" +
                          "\nClick for detailed view";

        TooltipText.Text = tooltipText;
    }

    private static Color GetCpuColor(double cpuUsage)
    {
        if (cpuUsage < LowThreshold)
        {
            return LowColor;
        }
        else if (cpuUsage < MediumThreshold)
        {
            // Interpolate between green and yellow
            var t = (cpuUsage - LowThreshold) / (MediumThreshold - LowThreshold);
            return InterpolateColor(LowColor, MediumColor, t);
        }
        else
        {
            // Interpolate between yellow and red
            var t = (cpuUsage - MediumThreshold) / (100.0 - MediumThreshold);
            return InterpolateColor(MediumColor, HighColor, t);
        }
    }

    private static Color InterpolateColor(Color from, Color to, double t)
    {
        t = Math.Max(0, Math.Min(1, t));
        return Color.FromRgb(
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t)
        );
    }

    private static Color GetRamColor(double ramUsage)
    {
        if (ramUsage < LowThreshold)
        {
            return RamLowColor;
        }
        else if (ramUsage < MediumThreshold)
        {
            // Interpolate between blue and yellow
            var t = (ramUsage - LowThreshold) / (MediumThreshold - LowThreshold);
            return InterpolateColor(RamLowColor, RamMediumColor, t);
        }
        else
        {
            // Interpolate between yellow and red
            var t = (ramUsage - MediumThreshold) / (100.0 - MediumThreshold);
            return InterpolateColor(RamMediumColor, RamHighColor, t);
        }
    }

    private static Color GetBufferColor(double bufferFill)
    {
        // Buffer color is inverted - low fill is bad (red), high fill is good (green)
        if (bufferFill >= 70)
        {
            return BufferOkColor;
        }
        else if (bufferFill >= 30)
        {
            // Interpolate between yellow and green
            var t = (bufferFill - 30) / 40.0;
            return InterpolateColor(BufferWarningColor, BufferOkColor, t);
        }
        else
        {
            // Interpolate between red and yellow
            var t = bufferFill / 30.0;
            return InterpolateColor(BufferCriticalColor, BufferWarningColor, t);
        }
    }

    #endregion

    #region Event Handlers

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenDetailsRequested?.Invoke(this, EventArgs.Empty);

        // If no handler is attached, try to open the dialog directly
        if (OpenDetailsRequested == null)
        {
            OpenPerformanceDialog();
        }
    }

    private void OpenPerformanceDialog()
    {
        try
        {
            var dialog = new PerformanceDialog
            {
                Owner = Window.GetWindow(this)
            };

            // Connect to the same view model if available
            if (_viewModel != null)
            {
                dialog.SetViewModel(_viewModel);
            }

            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open performance dialog: {ex.Message}");
        }
    }

    #endregion
}
