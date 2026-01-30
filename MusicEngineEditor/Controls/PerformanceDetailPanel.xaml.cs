// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Detailed performance monitoring panel.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Converter to subtract margin from width for proper header sizing.
/// </summary>
public class WidthMinusMarginConverter : IValueConverter
{
    public static readonly WidthMinusMarginConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double width)
        {
            return Math.Max(0, width - 40); // Account for padding and expander arrow
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Represents CPU usage data for a single track.
/// </summary>
public class TrackCpuUsage
{
    public string TrackName { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public double BarWidth => Math.Max(0, Math.Min(100, CpuPercent)); // Used for visual bar width
}

/// <summary>
/// Represents CPU and memory usage data for a plugin.
/// </summary>
public class PluginUsageInfo
{
    public string Name { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public double MemoryMB { get; set; }
}

/// <summary>
/// Expandable panel showing detailed performance metrics including
/// CPU history graph, per-track breakdown, and plugin usage.
/// </summary>
public partial class PerformanceDetailPanel : UserControl
{
    #region Constants

    private const int MaxHistoryPoints = 600; // 60 seconds at 10Hz
    private const int GraphUpdateInterval = 100; // ms

    private static readonly Color CpuColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color PeakColor = Color.FromRgb(0xFF, 0x47, 0x57);

    #endregion

    #region Private Fields

    private readonly DispatcherTimer _updateTimer;
    private readonly List<double> _cpuHistory = new();
    private readonly DateTime _startTime;

    private PerformanceViewModel? _viewModel;
    private double _peakCpu;
    private double _currentCpu;

    // Memory allocation tracking
    private double _processMemoryMB;
    private double _audioBufferMemoryMB;
    private double _pluginMemoryMB;
    private double _totalAvailableMemoryMB = 16384; // Default 16GB, will be updated

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the collection of per-track CPU usage data.
    /// </summary>
    public ObservableCollection<TrackCpuUsage> TrackCpuUsage { get; } = new();

    /// <summary>
    /// Gets or sets the collection of plugin CPU usage data.
    /// </summary>
    public ObservableCollection<PluginUsageInfo> PluginCpuUsage { get; } = new();

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(PerformanceDetailPanel),
            new PropertyMetadata(true, OnIsExpandedChanged));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceDetailPanel panel)
        {
            panel.MainExpander.IsExpanded = (bool)e.NewValue;
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the reset button is clicked.
    /// </summary>
    public event EventHandler? ResetRequested;

    #endregion

    #region Constructor

    public PerformanceDetailPanel()
    {
        InitializeComponent();

        _startTime = DateTime.Now;

        // Bind collections
        TrackCpuList.ItemsSource = TrackCpuUsage;
        PluginCpuGrid.ItemsSource = PluginCpuUsage;

        // Set up update timer for graph
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(GraphUpdateInterval)
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        // Handle size changes for graph
        CpuHistoryCanvas.SizeChanged += OnCanvasSizeChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Connects this panel to a PerformanceViewModel for automatic updates.
    /// </summary>
    public void ConnectToViewModel(PerformanceViewModel viewModel)
    {
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
    /// Updates the CPU usage value and adds to history.
    /// </summary>
    public void UpdateCpuUsage(double cpuPercent)
    {
        _currentCpu = cpuPercent;
        if (cpuPercent > _peakCpu)
        {
            _peakCpu = cpuPercent;
        }

        _cpuHistory.Add(cpuPercent);
        while (_cpuHistory.Count > MaxHistoryPoints)
        {
            _cpuHistory.RemoveAt(0);
        }

        CurrentCpuText.Text = $"{cpuPercent:F1}%";
        PeakCpuText.Text = $"{_peakCpu:F1}%";
    }

    /// <summary>
    /// Updates memory allocation display.
    /// </summary>
    public void UpdateMemoryAllocation(double processMemMB, double audioBufferMemMB, double pluginMemMB)
    {
        _processMemoryMB = processMemMB;
        _audioBufferMemoryMB = audioBufferMemMB;
        _pluginMemoryMB = pluginMemMB;

        UpdateMemoryBars();
    }

    /// <summary>
    /// Updates per-track CPU usage display.
    /// </summary>
    public void UpdateTrackCpuUsage(IEnumerable<TrackCpuUsage> trackUsage)
    {
        TrackCpuUsage.Clear();
        foreach (var track in trackUsage.OrderByDescending(t => t.CpuPercent))
        {
            TrackCpuUsage.Add(track);
        }
    }

    /// <summary>
    /// Updates plugin CPU usage display.
    /// </summary>
    public void UpdatePluginUsage(IEnumerable<PluginUsageInfo> pluginUsage)
    {
        PluginCpuUsage.Clear();
        foreach (var plugin in pluginUsage.OrderByDescending(p => p.CpuPercent))
        {
            PluginCpuUsage.Add(plugin);
        }
    }

    /// <summary>
    /// Resets all statistics and clears history.
    /// </summary>
    public void Reset()
    {
        _cpuHistory.Clear();
        _peakCpu = 0;
        _currentCpu = 0;

        CurrentCpuText.Text = "0%";
        PeakCpuText.Text = "0%";

        TrackCpuUsage.Clear();
        PluginCpuUsage.Clear();

        DrawCpuHistoryGraph();
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Start();
        DrawCpuHistoryGraph();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        DrawCpuHistoryGraph();
        UpdateUptime();

        // Update from ViewModel if connected
        if (_viewModel != null)
        {
            UpdateCpuUsage(_viewModel.CpuUsage);
        }
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawCpuHistoryGraph();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(PerformanceViewModel.CpuUsage):
                    UpdateCpuUsage(_viewModel?.CpuUsage ?? 0);
                    break;
                case nameof(PerformanceViewModel.MemoryUsageMB):
                    _processMemoryMB = _viewModel?.MemoryUsageMB ?? 0;
                    UpdateMemoryBars();
                    break;
                case nameof(PerformanceViewModel.PluginCpuUsage):
                    if (_viewModel != null)
                    {
                        UpdatePluginUsage(_viewModel.PluginCpuUsage.Select(p => new PluginUsageInfo
                        {
                            Name = p.Name,
                            CpuPercent = p.CpuUsage,
                            MemoryMB = 0 // Memory per plugin not tracked in ViewModel
                        }));
                    }
                    break;
            }
        });
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Private Methods

    private void UpdateFromViewModel()
    {
        if (_viewModel == null) return;

        UpdateCpuUsage(_viewModel.CpuUsage);
        _processMemoryMB = _viewModel.MemoryUsageMB;
        UpdateMemoryBars();
    }

    private void DrawCpuHistoryGraph()
    {
        if (CpuHistoryCanvas.ActualWidth <= 0 || CpuHistoryCanvas.ActualHeight <= 0)
            return;

        var width = CpuHistoryCanvas.ActualWidth;
        var height = CpuHistoryCanvas.ActualHeight;

        // Update grid lines
        GridLine75.X1 = 0;
        GridLine75.X2 = width;
        GridLine75.Y1 = GridLine75.Y2 = height * 0.25;

        GridLine50.X1 = 0;
        GridLine50.X2 = width;
        GridLine50.Y1 = GridLine50.Y2 = height * 0.5;

        GridLine25.X1 = 0;
        GridLine25.X2 = width;
        GridLine25.Y1 = GridLine25.Y2 = height * 0.75;

        // Draw CPU history line
        if (_cpuHistory.Count > 1)
        {
            var points = new PointCollection();
            var step = width / Math.Max(1, _cpuHistory.Count - 1);

            for (int i = 0; i < _cpuHistory.Count; i++)
            {
                var x = i * step;
                var y = height - (_cpuHistory[i] / 100.0 * height);
                points.Add(new Point(x, Math.Max(0, Math.Min(height, y))));
            }

            CpuHistoryLine.Points = points;

            // Update line color based on current CPU
            CpuHistoryLine.Stroke = new SolidColorBrush(GetCpuColor(_currentCpu));
        }

        // Draw peak line
        if (_peakCpu > 0)
        {
            var peakY = height - (_peakCpu / 100.0 * height);
            PeakLine.X1 = 0;
            PeakLine.X2 = width;
            PeakLine.Y1 = PeakLine.Y2 = peakY;
        }
    }

    private void UpdateMemoryBars()
    {
        var maxBarWidth = ProcessMemBar.Parent is Border container ? container.ActualWidth - 4 : 100;
        var maxMemory = Math.Max(_totalAvailableMemoryMB, _processMemoryMB + _audioBufferMemoryMB + _pluginMemoryMB);

        // Process memory bar
        ProcessMemBar.Width = Math.Max(0, (_processMemoryMB / maxMemory) * maxBarWidth);
        ProcessMemText.Text = FormatMemory(_processMemoryMB);

        // Audio buffer memory bar
        AudioBufferMemBar.Width = Math.Max(0, (_audioBufferMemoryMB / maxMemory) * maxBarWidth);
        AudioBufferMemText.Text = FormatMemory(_audioBufferMemoryMB);

        // Plugin memory bar
        PluginMemBar.Width = Math.Max(0, (_pluginMemoryMB / maxMemory) * maxBarWidth);
        PluginMemText.Text = FormatMemory(_pluginMemoryMB);
    }

    private void UpdateUptime()
    {
        var elapsed = DateTime.Now - _startTime;
        UptimeText.Text = elapsed.ToString(@"hh\:mm\:ss");
    }

    private static string FormatMemory(double memoryMB)
    {
        if (memoryMB >= 1024)
        {
            return $"{memoryMB / 1024:F1} GB";
        }
        return $"{memoryMB:F0} MB";
    }

    private static Color GetCpuColor(double cpuPercent)
    {
        if (cpuPercent < 50)
        {
            return CpuColor; // Green
        }
        else if (cpuPercent < 80)
        {
            // Yellow
            return Color.FromRgb(0xFF, 0xB8, 0x00);
        }
        else
        {
            return PeakColor; // Red
        }
    }

    #endregion
}
