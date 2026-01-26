// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Meter mode for mono or stereo operation.
/// </summary>
public enum VuMeterMode
{
    /// <summary>Stereo mode with left/right display.</summary>
    Stereo,
    /// <summary>Mono mode with single summed display.</summary>
    Mono
}

/// <summary>
/// A classic VU meter control with needle animation and proper ballistics.
/// Features -20 to +3 VU scale, peak LED indicator, and stereo/mono modes.
/// </summary>
public partial class VUMeterControl : UserControl
{
    #region Constants

    // VU meter standard: 0 VU = +4 dBu = -18 dBFS (broadcast standard)
    private const double VuMinDb = -20.0; // -20 VU
    private const double VuMaxDb = 3.0;   // +3 VU
    private const double VuRangeDb = VuMaxDb - VuMinDb; // 23 dB range
    private const double VuZeroDb = -18.0; // 0 VU in dBFS

    // Needle angle range (-45 to +45 degrees from center)
    private const double NeedleMinAngle = -45.0;
    private const double NeedleMaxAngle = 45.0;

    // VU ballistics: 300ms integration time (standard)
    private const double VuIntegrationTime = 0.3; // seconds
    private const double VuAttackCoeff = 0.01; // Fast attack
    private const double VuReleaseCoeff = 0.01; // Matches attack for symmetry

    // Peak LED hold time
    private const double PeakHoldTime = 2.0; // seconds

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty LeftLevelProperty =
        DependencyProperty.Register(nameof(LeftLevel), typeof(float), typeof(VUMeterControl),
            new PropertyMetadata(0f, OnLevelChanged));

    public static readonly DependencyProperty RightLevelProperty =
        DependencyProperty.Register(nameof(RightLevel), typeof(float), typeof(VUMeterControl),
            new PropertyMetadata(0f, OnLevelChanged));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(nameof(Mode), typeof(VuMeterMode), typeof(VUMeterControl),
            new PropertyMetadata(VuMeterMode.Stereo, OnModeChanged));

    public static readonly DependencyProperty ReferenceDbProperty =
        DependencyProperty.Register(nameof(ReferenceDb), typeof(double), typeof(VUMeterControl),
            new PropertyMetadata(-18.0));

    /// <summary>
    /// Gets or sets the left channel level (0.0 to 1.0+, linear).
    /// </summary>
    public float LeftLevel
    {
        get => (float)GetValue(LeftLevelProperty);
        set => SetValue(LeftLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the right channel level (0.0 to 1.0+, linear).
    /// </summary>
    public float RightLevel
    {
        get => (float)GetValue(RightLevelProperty);
        set => SetValue(RightLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the meter mode (Stereo or Mono).
    /// </summary>
    public VuMeterMode Mode
    {
        get => (VuMeterMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>
    /// Gets or sets the reference level in dBFS where 0 VU occurs.
    /// Default is -18 dBFS (broadcast standard).
    /// </summary>
    public double ReferenceDb
    {
        get => (double)GetValue(ReferenceDbProperty);
        set => SetValue(ReferenceDbProperty, value);
    }

    #endregion

    #region Private Fields

    private double _displayedLeftVu;
    private double _displayedRightVu;
    private double _targetLeftVu;
    private double _targetRightVu;
    private bool _leftPeakLit;
    private bool _rightPeakLit;
    private DateTime _leftPeakTime;
    private DateTime _rightPeakTime;

    private DispatcherTimer? _updateTimer;
    private DateTime _lastUpdateTime;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public VUMeterControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        PeakLed.MouseLeftButtonDown += OnPeakLedClick;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawMeterScale();
        DrawNeedle();
        UpdateModeDisplay();
        StartUpdateTimer();
        _isInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopUpdateTimer();
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawMeterScale();
            DrawNeedle();
            UpdateNeedlePosition();
        }
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VUMeterControl meter && meter._isInitialized)
        {
            meter.UpdateTargetLevels();
        }
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VUMeterControl meter && meter._isInitialized)
        {
            meter.UpdateModeDisplay();
        }
    }

    private void OnPeakLedClick(object sender, MouseButtonEventArgs e)
    {
        ResetPeakIndicators();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets both channel levels at once.
    /// </summary>
    public void SetLevels(float left, float right)
    {
        LeftLevel = left;
        RightLevel = right;
    }

    /// <summary>
    /// Resets the meter to zero and clears peak indicators.
    /// </summary>
    public void Reset()
    {
        LeftLevel = 0f;
        RightLevel = 0f;
        _displayedLeftVu = VuMinDb;
        _displayedRightVu = VuMinDb;
        _targetLeftVu = VuMinDb;
        _targetRightVu = VuMinDb;
        ResetPeakIndicators();
        UpdateNeedlePosition();
        UpdateDbDisplay();
    }

    /// <summary>
    /// Resets just the peak LED indicators.
    /// </summary>
    public void ResetPeakIndicators()
    {
        _leftPeakLit = false;
        _rightPeakLit = false;
        UpdatePeakLed();
    }

    #endregion

    #region Private Methods - Drawing

    private void DrawMeterScale()
    {
        if (MeterCanvas.ActualWidth <= 0 || MeterCanvas.ActualHeight <= 0)
            return;

        // Remove old scale elements
        var toRemove = new System.Collections.Generic.List<UIElement>();
        foreach (UIElement child in MeterCanvas.Children)
        {
            if (child is not Shapes.Path && child is not Shapes.Ellipse)
            {
                toRemove.Add(child);
            }
        }
        foreach (var child in toRemove)
        {
            MeterCanvas.Children.Remove(child);
        }

        double width = MeterCanvas.ActualWidth;
        double height = MeterCanvas.ActualHeight;
        double centerX = width / 2;
        double bottomY = height - 10;
        double arcRadius = Math.Min(width, height) * 0.7;

        var scaleBrush = FindResource("ScaleLineBrush") as Brush ?? Brushes.Gray;
        var textBrush = FindResource("TextPrimaryBrush") as Brush ?? Brushes.White;
        var zeroBrush = FindResource("ZeroMarkBrush") as Brush ?? Brushes.Orange;

        // VU scale marks: -20, -10, -7, -5, -3, -2, -1, 0, +1, +2, +3
        double[] vuMarks = { -20, -10, -7, -5, -3, -2, -1, 0, 1, 2, 3 };

        foreach (double vu in vuMarks)
        {
            double angle = VuToAngle(vu);
            double radians = angle * Math.PI / 180;

            // Calculate tick positions
            double innerRadius = arcRadius * 0.85;
            double outerRadius = arcRadius * 0.95;

            double x1 = centerX + innerRadius * Math.Sin(radians);
            double y1 = bottomY - innerRadius * Math.Cos(radians);
            double x2 = centerX + outerRadius * Math.Sin(radians);
            double y2 = bottomY - outerRadius * Math.Cos(radians);

            // Tick line
            var tick = new Shapes.Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = vu == 0 ? zeroBrush : scaleBrush,
                StrokeThickness = vu == 0 ? 2 : 1
            };
            MeterCanvas.Children.Add(tick);

            // Label (only for major marks)
            if (vu == -20 || vu == -10 || vu == -7 || vu == -5 || vu == -3 || vu == 0 || vu == 3)
            {
                double labelRadius = arcRadius * 0.75;
                double lx = centerX + labelRadius * Math.Sin(radians);
                double ly = bottomY - labelRadius * Math.Cos(radians);

                var label = new TextBlock
                {
                    Text = vu > 0 ? $"+{vu}" : vu.ToString(),
                    FontSize = vu == 0 ? 10 : 8,
                    FontWeight = vu == 0 ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = vu == 0 ? zeroBrush : textBrush
                };

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, lx - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, ly - label.DesiredSize.Height / 2);
                MeterCanvas.Children.Add(label);
            }
        }

        // Draw color zones arc (green, yellow, red)
        DrawColorZoneArc(centerX, bottomY, arcRadius);
    }

    private void DrawColorZoneArc(double centerX, double bottomY, double radius)
    {
        double arcRadius = radius * 0.98;
        double thickness = 4;

        // Green zone: -20 to -3 VU
        DrawArcSegment(centerX, bottomY, arcRadius, VuToAngle(-20), VuToAngle(-3),
            new SolidColorBrush(Color.FromRgb(0x00, 0xAA, 0x00)), thickness);

        // Yellow zone: -3 to 0 VU
        DrawArcSegment(centerX, bottomY, arcRadius, VuToAngle(-3), VuToAngle(0),
            new SolidColorBrush(Color.FromRgb(0xCC, 0xAA, 0x00)), thickness);

        // Red zone: 0 to +3 VU
        DrawArcSegment(centerX, bottomY, arcRadius, VuToAngle(0), VuToAngle(3),
            new SolidColorBrush(Color.FromRgb(0xCC, 0x00, 0x00)), thickness);
    }

    private void DrawArcSegment(double centerX, double centerY, double radius,
        double startAngle, double endAngle, Brush brush, double thickness)
    {
        double startRad = startAngle * Math.PI / 180;
        double endRad = endAngle * Math.PI / 180;

        double x1 = centerX + radius * Math.Sin(startRad);
        double y1 = centerY - radius * Math.Cos(startRad);
        double x2 = centerX + radius * Math.Sin(endRad);
        double y2 = centerY - radius * Math.Cos(endRad);

        var arc = new Shapes.Path
        {
            Stroke = brush,
            StrokeThickness = thickness,
            Data = new PathGeometry(new[]
            {
                new PathFigure(new Point(x1, y1), new PathSegment[]
                {
                    new ArcSegment(new Point(x2, y2), new Size(radius, radius),
                        0, false, SweepDirection.Clockwise, true)
                }, false)
            })
        };

        MeterCanvas.Children.Add(arc);
    }

    private void DrawNeedle()
    {
        if (MeterCanvas.ActualWidth <= 0 || MeterCanvas.ActualHeight <= 0)
            return;

        double width = MeterCanvas.ActualWidth;
        double height = MeterCanvas.ActualHeight;
        double centerX = width / 2;
        double bottomY = height - 10;
        double needleLength = Math.Min(width, height) * 0.65;

        // Create needle geometry
        var needleGeometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(centerX - 2, bottomY),
            IsClosed = true
        };
        figure.Segments.Add(new LineSegment(new Point(centerX, bottomY - needleLength), true));
        figure.Segments.Add(new LineSegment(new Point(centerX + 2, bottomY), true));
        needleGeometry.Figures.Add(figure);

        Needle.Data = needleGeometry;
        NeedleShadow.Data = needleGeometry;

        // Position pivot
        Canvas.SetLeft(NeedlePivot, centerX - NeedlePivot.Width / 2);
        Canvas.SetTop(NeedlePivot, bottomY - NeedlePivot.Height / 2);

        // Set transform origin
        Needle.RenderTransformOrigin = new Point(0.5, 1);
        NeedleShadow.RenderTransformOrigin = new Point(0.5, 1);
    }

    #endregion

    #region Private Methods - Level Processing

    private void UpdateTargetLevels()
    {
        // Convert linear levels to dBFS
        double leftDb = LinearToDb(LeftLevel);
        double rightDb = LinearToDb(RightLevel);

        // Convert dBFS to VU (relative to reference level)
        _targetLeftVu = leftDb - ReferenceDb;
        _targetRightVu = rightDb - ReferenceDb;

        // Check for peak (over 0 VU)
        if (_targetLeftVu > 0)
        {
            _leftPeakLit = true;
            _leftPeakTime = DateTime.UtcNow;
        }
        if (_targetRightVu > 0)
        {
            _rightPeakLit = true;
            _rightPeakTime = DateTime.UtcNow;
        }
    }

    private void ProcessBallistics(double deltaTime)
    {
        // VU meter ballistics: slow integration (300ms)
        double alpha = 1.0 - Math.Exp(-deltaTime / VuIntegrationTime);

        // In mono mode, average the channels
        double targetVu;
        if (Mode == VuMeterMode.Mono)
        {
            targetVu = (_targetLeftVu + _targetRightVu) / 2;
            _displayedLeftVu = _displayedLeftVu + alpha * (targetVu - _displayedLeftVu);
            _displayedRightVu = _displayedLeftVu;
        }
        else
        {
            _displayedLeftVu = _displayedLeftVu + alpha * (_targetLeftVu - _displayedLeftVu);
            _displayedRightVu = _displayedRightVu + alpha * (_targetRightVu - _displayedRightVu);
        }

        // Clamp to VU range
        _displayedLeftVu = Math.Max(VuMinDb, Math.Min(VuMaxDb, _displayedLeftVu));
        _displayedRightVu = Math.Max(VuMinDb, Math.Min(VuMaxDb, _displayedRightVu));

        // Auto-reset peak LEDs after hold time
        var now = DateTime.UtcNow;
        if (_leftPeakLit && (now - _leftPeakTime).TotalSeconds > PeakHoldTime)
        {
            _leftPeakLit = false;
        }
        if (_rightPeakLit && (now - _rightPeakTime).TotalSeconds > PeakHoldTime)
        {
            _rightPeakLit = false;
        }
    }

    #endregion

    #region Private Methods - Visual Updates

    private void UpdateNeedlePosition()
    {
        // Use average for needle (or left in stereo if we want two needles)
        double displayVu = Mode == VuMeterMode.Mono
            ? _displayedLeftVu
            : (_displayedLeftVu + _displayedRightVu) / 2;

        double angle = VuToAngle(displayVu);
        NeedleRotation.Angle = angle;
        NeedleShadowRotation.Angle = angle + 2; // Slight offset for shadow
    }

    private void UpdateDbDisplay()
    {
        double leftDb = _displayedLeftVu + ReferenceDb;
        double rightDb = _displayedRightVu + ReferenceDb;

        if (leftDb <= -60)
            LeftDbText.Text = "-inf";
        else
            LeftDbText.Text = $"{leftDb:F1}";

        if (rightDb <= -60)
            RightDbText.Text = "-inf";
        else
            RightDbText.Text = $"{rightDb:F1}";
    }

    private void UpdatePeakLed()
    {
        bool anyPeak = _leftPeakLit || _rightPeakLit;
        var onBrush = FindResource("PeakLedOnBrush") as Brush;
        var offBrush = FindResource("PeakLedOffBrush") as Brush;

        PeakLed.Fill = anyPeak ? onBrush : offBrush;
        PeakLed.Effect = anyPeak ? FindResource("PeakGlowEffect") as Effect : null;
    }

    private void UpdateModeDisplay()
    {
        if (Mode == VuMeterMode.Mono)
        {
            ChannelLabel.Text = "Mono";
            RightDbPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            ChannelLabel.Text = "L/R";
            RightDbPanel.Visibility = Visibility.Visible;
        }
    }

    #endregion

    #region Private Methods - Timer

    private void StartUpdateTimer()
    {
        _lastUpdateTime = DateTime.UtcNow;
        _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS for smooth needle
        };
        _updateTimer.Tick += OnUpdateTimerTick;
        _updateTimer.Start();
    }

    private void StopUpdateTimer()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= OnUpdateTimerTick;
            _updateTimer = null;
        }
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double deltaTime = (now - _lastUpdateTime).TotalSeconds;
        _lastUpdateTime = now;

        deltaTime = Math.Min(deltaTime, 0.1);

        ProcessBallistics(deltaTime);
        UpdateNeedlePosition();
        UpdateDbDisplay();
        UpdatePeakLed();
    }

    #endregion

    #region Private Methods - Utility

    private static double LinearToDb(float linear)
    {
        if (linear <= 0)
            return -60.0;
        return 20.0 * Math.Log10(linear);
    }

    private static double VuToAngle(double vu)
    {
        // Map VU range to angle range
        double normalized = (vu - VuMinDb) / VuRangeDb;
        normalized = Math.Max(0, Math.Min(1, normalized));
        return NeedleMinAngle + normalized * (NeedleMaxAngle - NeedleMinAngle);
    }

    #endregion
}
