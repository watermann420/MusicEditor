using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A compact horizontal performance meter showing CPU and memory usage.
/// Click to open a detailed performance dialog.
/// </summary>
public partial class PerformanceMeter : UserControl
{
    #region Constants

    private const double LowThreshold = 50.0;   // 0-50% = Green
    private const double MediumThreshold = 80.0; // 50-80% = Yellow
    // 80-100% = Red

    private static readonly Color LowColor = Color.FromRgb(0x6A, 0xAB, 0x73);    // Green
    private static readonly Color MediumColor = Color.FromRgb(0xE8, 0xB3, 0x39); // Yellow
    private static readonly Color HighColor = Color.FromRgb(0xF7, 0x54, 0x64);   // Red

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
        MemoryText.Text = $"{MemoryUsageMB:F0} MB";
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

    private void UpdateTooltip()
    {
        var tooltipText = $"CPU Usage: {CpuUsage:F1}%\n" +
                          $"  Peak: {PeakCpuUsage:F1}%\n" +
                          $"  Average: {AverageCpuUsage:F1}%\n" +
                          $"\nMemory: {MemoryUsageMB:F1} MB\n" +
                          $"  Peak: {PeakMemoryUsageMB:F1} MB\n" +
                          $"\nBuffer Underruns: {DropoutCount}\n" +
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
