// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents an automation point with time and value for preview display.
/// </summary>
public class PreviewAutomationPoint
{
    public double Time { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// Automation preview control for comparing current and pending automation changes.
/// Supports A/B toggle, visual difference display, and partial apply options.
/// </summary>
public partial class AutomationPreviewControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty CurrentPointsProperty =
        DependencyProperty.Register(nameof(CurrentPoints), typeof(IList<PreviewAutomationPoint>), typeof(AutomationPreviewControl),
            new PropertyMetadata(null, OnPointsChanged));

    public static readonly DependencyProperty PreviewPointsProperty =
        DependencyProperty.Register(nameof(PreviewPoints), typeof(IList<PreviewAutomationPoint>), typeof(AutomationPreviewControl),
            new PropertyMetadata(null, OnPointsChanged));

    public static readonly DependencyProperty ParameterNameProperty =
        DependencyProperty.Register(nameof(ParameterName), typeof(string), typeof(AutomationPreviewControl),
            new PropertyMetadata("Parameter", OnParameterNameChanged));

    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(AutomationPreviewControl),
            new PropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(AutomationPreviewControl),
            new PropertyMetadata(1.0, OnRangeChanged));

    public static readonly DependencyProperty TotalTimeProperty =
        DependencyProperty.Register(nameof(TotalTime), typeof(double), typeof(AutomationPreviewControl),
            new PropertyMetadata(240.0, OnRangeChanged)); // 4 minutes default

    /// <summary>
    /// Gets or sets the current automation points.
    /// </summary>
    public IList<PreviewAutomationPoint>? CurrentPoints
    {
        get => (IList<PreviewAutomationPoint>?)GetValue(CurrentPointsProperty);
        set => SetValue(CurrentPointsProperty, value);
    }

    /// <summary>
    /// Gets or sets the preview automation points.
    /// </summary>
    public IList<PreviewAutomationPoint>? PreviewPoints
    {
        get => (IList<PreviewAutomationPoint>?)GetValue(PreviewPointsProperty);
        set => SetValue(PreviewPointsProperty, value);
    }

    /// <summary>
    /// Gets or sets the parameter name being automated.
    /// </summary>
    public string ParameterName
    {
        get => (string)GetValue(ParameterNameProperty);
        set => SetValue(ParameterNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum parameter value.
    /// </summary>
    public double MinValue
    {
        get => (double)GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum parameter value.
    /// </summary>
    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the total time range in seconds.
    /// </summary>
    public double TotalTime
    {
        get => (double)GetValue(TotalTimeProperty);
        set => SetValue(TotalTimeProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the user clicks Apply.
    /// </summary>
    public event EventHandler<AutomationApplyEventArgs>? ApplyRequested;

    /// <summary>
    /// Raised when the user clicks Revert.
    /// </summary>
    public event EventHandler? RevertRequested;

    /// <summary>
    /// Raised when A/B state changes.
    /// </summary>
    public event EventHandler<bool>? ABStateChanged;

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private bool _showPreview;
    private bool _partialApply;
    private double _partialStartTime;
    private double _partialEndTime;

    private readonly Color _currentColor = Color.FromRgb(0x00, 0xCE, 0xD1);
    private readonly Color _previewColor = Color.FromRgb(0xFF, 0x98, 0x00);
    private readonly Color _addColor = Color.FromRgb(0x00, 0xCC, 0x66);
    private readonly Color _removeColor = Color.FromRgb(0xFF, 0x47, 0x57);

    #endregion

    #region Constructor

    public AutomationPreviewControl()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        _partialEndTime = TotalTime;
        EndTimeText.Text = FormatTime(_partialEndTime);
        DrawAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawAll();
        }
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationPreviewControl control && control._isInitialized)
        {
            control.DrawAll();
            control.UpdateStatistics();
        }
    }

    private static void OnParameterNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationPreviewControl control)
        {
            control.ParameterNameText.Text = $" - {e.NewValue}";
        }
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationPreviewControl control && control._isInitialized)
        {
            control.DrawAll();
        }
    }

    private void ABToggle_Changed(object sender, RoutedEventArgs e)
    {
        _showPreview = ABToggle.IsChecked ?? false;
        UpdateCurveVisibility();
        ABStateChanged?.Invoke(this, _showPreview);
    }

    private void PartialApplyCheck_Changed(object sender, RoutedEventArgs e)
    {
        _partialApply = PartialApplyCheck.IsChecked ?? false;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ParseTimeRange();

        var args = new AutomationApplyEventArgs
        {
            IsPartialApply = _partialApply,
            StartTime = _partialStartTime,
            EndTime = _partialEndTime
        };

        ApplyRequested?.Invoke(this, args);
    }

    private void RevertButton_Click(object sender, RoutedEventArgs e)
    {
        RevertRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Drawing

    private void DrawAll()
    {
        DrawGrid();
        DrawValueScale();
        DrawDifference();
        DrawCurrentCurve();
        DrawPreviewCurve();
        UpdateCurveVisibility();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Vertical time divisions
        int timeDivisions = 8;
        for (int i = 0; i <= timeDivisions; i++)
        {
            double x = i * width / timeDivisions;
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }

        // Horizontal value divisions
        int valueDivisions = 4;
        for (int i = 0; i <= valueDivisions; i++)
        {
            double y = i * height / valueDivisions;
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawValueScale()
    {
        ValueScaleCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double height = GridCanvas.ActualHeight;

        if (height <= 0) return;

        // Value labels
        int divisions = 4;
        for (int i = 0; i <= divisions; i++)
        {
            double value = MaxValue - (MaxValue - MinValue) * i / divisions;
            double y = i * height / divisions;

            var label = new TextBlock
            {
                Text = value.ToString("F2"),
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 4);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            ValueScaleCanvas.Children.Add(label);
        }
    }

    private void DrawDifference()
    {
        DifferenceCanvas.Children.Clear();

        if (CurrentPoints == null || PreviewPoints == null) return;

        double width = DifferenceCanvas.ActualWidth;
        double height = DifferenceCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Sample both curves at regular intervals and show difference
        int samples = (int)width;
        var addGeometry = new StreamGeometry();
        var removeGeometry = new StreamGeometry();

        using (var addContext = addGeometry.Open())
        using (var removeContext = removeGeometry.Open())
        {
            bool addStarted = false;
            bool removeStarted = false;
            double lastAddX = 0, lastRemoveX = 0;
            double centerY = height / 2;

            for (int i = 0; i < samples; i++)
            {
                double time = (double)i / samples * TotalTime;
                double currentValue = InterpolateValue(CurrentPoints, time);
                double previewValue = InterpolateValue(PreviewPoints, time);

                double x = i;
                double currentY = ValueToY(currentValue, height);
                double previewY = ValueToY(previewValue, height);

                if (previewY < currentY) // Preview is higher (add)
                {
                    if (!addStarted)
                    {
                        addContext.BeginFigure(new Point(x, currentY), true, true);
                        addStarted = true;
                    }
                    addContext.LineTo(new Point(x, previewY), true, false);
                    lastAddX = x;
                }

                if (previewY > currentY) // Preview is lower (remove)
                {
                    if (!removeStarted)
                    {
                        removeContext.BeginFigure(new Point(x, currentY), true, true);
                        removeStarted = true;
                    }
                    removeContext.LineTo(new Point(x, previewY), true, false);
                    lastRemoveX = x;
                }
            }
        }

        addGeometry.Freeze();
        removeGeometry.Freeze();

        // Add fill (green - preview is higher/more)
        var addPath = new Shapes.Path
        {
            Data = addGeometry,
            Fill = new SolidColorBrush(Color.FromArgb(64, _addColor.R, _addColor.G, _addColor.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(128, _addColor.R, _addColor.G, _addColor.B)),
            StrokeThickness = 1
        };
        DifferenceCanvas.Children.Add(addPath);

        // Remove fill (red - preview is lower/less)
        var removePath = new Shapes.Path
        {
            Data = removeGeometry,
            Fill = new SolidColorBrush(Color.FromArgb(64, _removeColor.R, _removeColor.G, _removeColor.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(128, _removeColor.R, _removeColor.G, _removeColor.B)),
            StrokeThickness = 1
        };
        DifferenceCanvas.Children.Add(removePath);
    }

    private void DrawCurrentCurve()
    {
        CurrentCurveCanvas.Children.Clear();

        if (CurrentPoints == null || CurrentPoints.Count == 0) return;

        double width = CurrentCurveCanvas.ActualWidth;
        double height = CurrentCurveCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        DrawCurve(CurrentCurveCanvas, CurrentPoints, _currentColor, width, height);
    }

    private void DrawPreviewCurve()
    {
        PreviewCurveCanvas.Children.Clear();

        if (PreviewPoints == null || PreviewPoints.Count == 0) return;

        double width = PreviewCurveCanvas.ActualWidth;
        double height = PreviewCurveCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        DrawCurve(PreviewCurveCanvas, PreviewPoints, _previewColor, width, height);
    }

    private void DrawCurve(Canvas canvas, IList<PreviewAutomationPoint> points, Color color, double width, double height)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            bool started = false;

            foreach (var point in points)
            {
                double x = TimeToX(point.Time, width);
                double y = ValueToY(point.Value, height);

                if (!started)
                {
                    context.BeginFigure(new Point(x, y), false, false);
                    started = true;
                }
                else
                {
                    context.LineTo(new Point(x, y), true, true);
                }
            }
        }

        geometry.Freeze();

        var path = new Shapes.Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        canvas.Children.Add(path);

        // Draw points
        foreach (var point in points)
        {
            double x = TimeToX(point.Time, width);
            double y = ValueToY(point.Value, height);

            var dot = new Shapes.Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            Canvas.SetLeft(dot, x - 3);
            Canvas.SetTop(dot, y - 3);
            canvas.Children.Add(dot);
        }
    }

    private void UpdateCurveVisibility()
    {
        // Both curves always visible, but we could emphasize one
        CurrentCurveCanvas.Opacity = _showPreview ? 0.5 : 1.0;
        PreviewCurveCanvas.Opacity = _showPreview ? 1.0 : 0.5;
    }

    #endregion

    #region Statistics

    private void UpdateStatistics()
    {
        if (CurrentPoints == null || PreviewPoints == null)
        {
            PointsChangedText.Text = "Points changed: 0";
            MaxDifferenceText.Text = "Max diff: 0.0";
            return;
        }

        int pointsChanged = 0;
        double maxDiff = 0;

        // Compare point counts
        pointsChanged = Math.Abs(PreviewPoints.Count - CurrentPoints.Count);

        // Find max difference by sampling
        int samples = 100;
        for (int i = 0; i < samples; i++)
        {
            double time = (double)i / samples * TotalTime;
            double currentValue = InterpolateValue(CurrentPoints, time);
            double previewValue = InterpolateValue(PreviewPoints, time);
            double diff = Math.Abs(previewValue - currentValue);
            if (diff > maxDiff) maxDiff = diff;
        }

        // Count significant point changes
        foreach (var previewPoint in PreviewPoints)
        {
            bool found = false;
            foreach (var currentPoint in CurrentPoints)
            {
                if (Math.Abs(currentPoint.Time - previewPoint.Time) < 0.1 &&
                    Math.Abs(currentPoint.Value - previewPoint.Value) < 0.01)
                {
                    found = true;
                    break;
                }
            }
            if (!found) pointsChanged++;
        }

        PointsChangedText.Text = $"Points changed: {pointsChanged}";
        MaxDifferenceText.Text = $"Max diff: {maxDiff:F2}";
    }

    #endregion

    #region Helper Methods

    private double TimeToX(double time, double width)
    {
        return (time / TotalTime) * width;
    }

    private double ValueToY(double value, double height)
    {
        double normalized = (value - MinValue) / (MaxValue - MinValue);
        return height * (1 - normalized);
    }

    private double InterpolateValue(IList<PreviewAutomationPoint>? points, double time)
    {
        if (points == null || points.Count == 0) return MinValue;
        if (points.Count == 1) return points[0].Value;

        // Find surrounding points
        PreviewAutomationPoint? before = null;
        PreviewAutomationPoint? after = null;

        foreach (var point in points)
        {
            if (point.Time <= time)
            {
                before = point;
            }
            if (point.Time >= time && after == null)
            {
                after = point;
                break;
            }
        }

        if (before == null) return points[0].Value;
        if (after == null) return points[points.Count - 1].Value;
        if (before == after) return before.Value;

        // Linear interpolation
        double t = (time - before.Time) / (after.Time - before.Time);
        return before.Value + t * (after.Value - before.Value);
    }

    private void ParseTimeRange()
    {
        _partialStartTime = ParseTimeText(StartTimeText.Text);
        _partialEndTime = ParseTimeText(EndTimeText.Text);
    }

    private static double ParseTimeText(string text)
    {
        // Parse M:SS or MM:SS format
        var parts = text.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
        {
            return minutes * 60 + seconds;
        }
        return 0;
    }

    private static string FormatTime(double seconds)
    {
        int mins = (int)(seconds / 60);
        int secs = (int)(seconds % 60);
        return $"{mins}:{secs:D2}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets current and preview automation data.
    /// </summary>
    public void SetAutomationData(IList<PreviewAutomationPoint> current, IList<PreviewAutomationPoint> preview)
    {
        CurrentPoints = current;
        PreviewPoints = preview;
    }

    /// <summary>
    /// Clears all automation data.
    /// </summary>
    public void Clear()
    {
        CurrentPoints = null;
        PreviewPoints = null;
    }

    /// <summary>
    /// Gets whether preview mode is active.
    /// </summary>
    public bool IsPreviewActive => _showPreview;

    #endregion
}

/// <summary>
/// Event args for automation apply request.
/// </summary>
public class AutomationApplyEventArgs : EventArgs
{
    public bool IsPartialApply { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}
