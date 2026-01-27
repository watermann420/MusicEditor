// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Converter for percentage to width (for plugin CPU bars).
/// </summary>
public class PercentToWidthConverter : IValueConverter
{
    public static readonly PercentToWidthConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            // Max width is 140 (from XAML)
            return Math.Max(0, Math.Min(140, percent * 1.4));
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Detailed performance monitoring dialog with real-time CPU graph and statistics.
/// </summary>
public partial class PerformanceDialog : Window
{
    #region Constants

    private const int MaxGraphPoints = 300; // 30 seconds at 10Hz
    private const double GraphPadding = 35.0;

    private static readonly Color LowColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color MediumColor = Color.FromRgb(0xFF, 0xB8, 0x00);
    private static readonly Color HighColor = Color.FromRgb(0xFF, 0x47, 0x57);
    private static readonly Color GraphLineColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color GraphFillColor = Color.FromArgb(0x33, 0x00, 0xD9, 0xFF);
    private static readonly Color GridLineColor = Color.FromRgb(0x2A, 0x2A, 0x2A);

    #endregion

    #region Private Fields

    private PerformanceViewModel? _viewModel;
    private readonly List<double> _cpuHistory = new();
    private readonly DispatcherTimer _updateTimer;
    private Polyline? _graphLine;
    private Polygon? _graphFill;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public PerformanceDialog()
    {
        InitializeComponent();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the view model for data binding.
    /// </summary>
    public void SetViewModel(PerformanceViewModel viewModel)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            PluginListView.ItemsSource = _viewModel.PluginCpuUsage;

            // Copy existing history
            _cpuHistory.Clear();
            foreach (var point in _viewModel.CpuHistory)
            {
                _cpuHistory.Add(point.Value);
            }

            UpdateStats();
            UpdateGraph();
        }
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        InitializeGraph();
        _updateTimer.Start();

        // Start monitoring if we have a view model
        _viewModel?.StartMonitoringCommand.Execute(null);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawGridLines();
            UpdateGraph();
        }
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            // Add current CPU to history
            _cpuHistory.Add(_viewModel.CpuUsage);

            // Trim old data
            while (_cpuHistory.Count > MaxGraphPoints)
            {
                _cpuHistory.RemoveAt(0);
            }

            UpdateStats();
            UpdateGraph();
            LastUpdateText.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateStats);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _cpuHistory.Clear();
        _viewModel?.ResetStatsCommand.Execute(null);
        UpdateGraph();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Graph Methods

    private void InitializeGraph()
    {
        GraphCanvas.Children.Clear();

        // Create the fill polygon
        _graphFill = new Polygon
        {
            Fill = new SolidColorBrush(GraphFillColor),
            StrokeThickness = 0
        };
        GraphCanvas.Children.Add(_graphFill);

        // Create the line
        _graphLine = new Polyline
        {
            Stroke = new SolidColorBrush(GraphLineColor),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        GraphCanvas.Children.Add(_graphLine);

        DrawGridLines();
        DrawScaleLabels();
    }

    private void DrawGridLines()
    {
        GridCanvas.Children.Clear();

        var width = GridCanvas.ActualWidth;
        var height = GridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var gridBrush = new SolidColorBrush(GridLineColor);

        // Horizontal grid lines (at 25%, 50%, 75%, 100%)
        for (int i = 1; i <= 4; i++)
        {
            var y = height - (height * i / 4);
            var line = new Line
            {
                X1 = GraphPadding,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }

        // Vertical grid lines (every 5 seconds)
        var secondsPerLine = 5.0;
        var pixelsPerSecond = (width - GraphPadding) / 30.0;
        for (double seconds = secondsPerLine; seconds <= 30; seconds += secondsPerLine)
        {
            var x = width - (seconds * pixelsPerSecond);
            if (x < GraphPadding) continue;

            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawScaleLabels()
    {
        ScaleCanvas.Children.Clear();

        var height = ScaleCanvas.ActualHeight;
        if (height <= 0) return;

        var labelBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));

        // Labels at 0%, 50%, 100%
        string[] labels = { "100%", "50%", "0%" };
        double[] positions = { 0, 0.5, 1 };

        for (int i = 0; i < labels.Length; i++)
        {
            var y = height * positions[i];
            var textBlock = new TextBlock
            {
                Text = labels[i],
                Foreground = labelBrush,
                FontSize = 9,
                FontFamily = new FontFamily("JetBrains Mono, Consolas")
            };

            Canvas.SetRight(textBlock, 4);
            Canvas.SetTop(textBlock, y - 6);
            ScaleCanvas.Children.Add(textBlock);
        }
    }

    private void UpdateGraph()
    {
        if (_graphLine == null || _graphFill == null) return;

        var width = GraphCanvas.ActualWidth - GraphPadding;
        var height = GraphCanvas.ActualHeight;

        if (width <= 0 || height <= 0 || _cpuHistory.Count == 0)
        {
            _graphLine.Points.Clear();
            _graphFill.Points.Clear();
            return;
        }

        var points = new PointCollection();
        var fillPoints = new PointCollection();

        var pointCount = _cpuHistory.Count;
        var pixelsPerPoint = width / MaxGraphPoints;

        // Start fill at bottom-right
        fillPoints.Add(new Point(GraphPadding + width, height));

        for (int i = 0; i < pointCount; i++)
        {
            var x = GraphPadding + width - ((pointCount - 1 - i) * pixelsPerPoint);
            var y = height - (height * Math.Min(100, _cpuHistory[i]) / 100.0);

            points.Add(new Point(x, y));
            fillPoints.Add(new Point(x, y));
        }

        // Complete fill polygon by adding bottom-left corner
        if (fillPoints.Count > 1)
        {
            fillPoints.Add(new Point(fillPoints[fillPoints.Count - 1].X, height));
        }

        _graphLine.Points = points;
        _graphFill.Points = fillPoints;

        // Update line color based on current CPU
        if (_cpuHistory.Count > 0)
        {
            var currentCpu = _cpuHistory[^1];
            var color = GetCpuColor(currentCpu);
            _graphLine.Stroke = new SolidColorBrush(color);
            _graphFill.Fill = new SolidColorBrush(Color.FromArgb(0x33, color.R, color.G, color.B));
        }
    }

    #endregion

    #region Stats Methods

    private void UpdateStats()
    {
        if (_viewModel == null) return;

        // CPU stats
        CpuCurrentText.Text = _viewModel.CpuUsageFormatted;
        CpuCurrentText.Foreground = new SolidColorBrush(GetCpuColor(_viewModel.CpuUsage));

        CpuPeakText.Text = _viewModel.PeakCpuUsageFormatted;
        CpuPeakText.Foreground = new SolidColorBrush(GetCpuColor(_viewModel.PeakCpuUsage));

        CpuAverageText.Text = _viewModel.AverageCpuUsageFormatted;

        // Memory stats
        MemoryCurrentText.Text = _viewModel.MemoryUsageFormatted;
        MemoryPeakText.Text = _viewModel.PeakMemoryUsageFormatted;

        // Audio stats
        LatencyText.Text = _viewModel.LatencyFormatted;
        BufferSizeText.Text = _viewModel.BufferSizeFormatted;
        SampleRateText.Text = _viewModel.SampleRateFormatted;

        // Dropout count with color
        DropoutText.Text = _viewModel.DropoutCountFormatted;
        DropoutText.Foreground = _viewModel.DropoutCount > 0
            ? new SolidColorBrush(HighColor)
            : new SolidColorBrush(LowColor);

        // Plugin list visibility
        NoPluginsText.Visibility = _viewModel.PluginCpuUsage.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static Color GetCpuColor(double cpuUsage)
    {
        if (cpuUsage < 50)
        {
            return LowColor;
        }
        else if (cpuUsage < 80)
        {
            var t = (cpuUsage - 50) / 30.0;
            return InterpolateColor(LowColor, MediumColor, t);
        }
        else
        {
            var t = (cpuUsage - 80) / 20.0;
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
}
