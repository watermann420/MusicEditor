// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Compressor/dynamics transfer function graph control.
/// Shows input vs output graph with threshold, ratio, knee display and operating point.
/// </summary>
public partial class DynamicsGraphControl : UserControl
{
    #region Constants

    private const double MinDb = -60.0;
    private const double MaxDb = 0.0;
    private const int CurveResolution = 100;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty ThresholdProperty =
        DependencyProperty.Register(nameof(Threshold), typeof(double), typeof(DynamicsGraphControl),
            new PropertyMetadata(-20.0, OnParameterChanged));

    public static readonly DependencyProperty RatioProperty =
        DependencyProperty.Register(nameof(Ratio), typeof(double), typeof(DynamicsGraphControl),
            new PropertyMetadata(4.0, OnParameterChanged));

    public static readonly DependencyProperty KneeWidthProperty =
        DependencyProperty.Register(nameof(KneeWidth), typeof(double), typeof(DynamicsGraphControl),
            new PropertyMetadata(6.0, OnParameterChanged));

    public static readonly DependencyProperty AttackMsProperty =
        DependencyProperty.Register(nameof(AttackMs), typeof(double), typeof(DynamicsGraphControl),
            new PropertyMetadata(10.0, OnParameterChanged));

    public static readonly DependencyProperty ReleaseMsProperty =
        DependencyProperty.Register(nameof(ReleaseMs), typeof(double), typeof(DynamicsGraphControl),
            new PropertyMetadata(100.0, OnParameterChanged));

    public static readonly DependencyProperty InputLevelProperty =
        DependencyProperty.Register(nameof(InputLevel), typeof(double), typeof(DynamicsGraphControl),
            new PropertyMetadata(-60.0, OnInputLevelChanged));

    public static readonly DependencyProperty GainReductionProperty =
        DependencyProperty.Register(nameof(GainReduction), typeof(double), typeof(DynamicsGraphControl),
            new PropertyMetadata(0.0, OnGainReductionChanged));

    public static readonly DependencyProperty MakeupGainProperty =
        DependencyProperty.Register(nameof(MakeupGain), typeof(double), typeof(DynamicsGraphControl),
            new PropertyMetadata(0.0, OnParameterChanged));

    /// <summary>
    /// Gets or sets the threshold in dB.
    /// </summary>
    public double Threshold
    {
        get => (double)GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    /// <summary>
    /// Gets or sets the compression ratio (e.g., 4 for 4:1).
    /// </summary>
    public double Ratio
    {
        get => (double)GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    /// <summary>
    /// Gets or sets the knee width in dB.
    /// </summary>
    public double KneeWidth
    {
        get => (double)GetValue(KneeWidthProperty);
        set => SetValue(KneeWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the attack time in milliseconds.
    /// </summary>
    public double AttackMs
    {
        get => (double)GetValue(AttackMsProperty);
        set => SetValue(AttackMsProperty, value);
    }

    /// <summary>
    /// Gets or sets the release time in milliseconds.
    /// </summary>
    public double ReleaseMs
    {
        get => (double)GetValue(ReleaseMsProperty);
        set => SetValue(ReleaseMsProperty, value);
    }

    /// <summary>
    /// Gets or sets the current input level in dB.
    /// </summary>
    public double InputLevel
    {
        get => (double)GetValue(InputLevelProperty);
        set => SetValue(InputLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the current gain reduction in dB (negative value).
    /// </summary>
    public double GainReduction
    {
        get => (double)GetValue(GainReductionProperty);
        set => SetValue(GainReductionProperty, value);
    }

    /// <summary>
    /// Gets or sets the makeup gain in dB.
    /// </summary>
    public double MakeupGain
    {
        get => (double)GetValue(MakeupGainProperty);
        set => SetValue(MakeupGainProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private double _displayedGainReduction;
    private DispatcherTimer? _animationTimer;

    private readonly Color _transferCurveColor = Color.FromRgb(0x00, 0xCE, 0xD1);
    private readonly Color _thresholdColor = Color.FromRgb(0xE8, 0x9C, 0x4B);
    private readonly Color _kneeColor = Color.FromRgb(0x00, 0xCC, 0x66);
    private readonly Color _operatingPointColor = Color.FromRgb(0xFF, 0x55, 0x55);

    #endregion

    #region Constructor

    public DynamicsGraphControl()
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
        DrawAll();
        StartAnimationTimer();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        StopAnimationTimer();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawAll();
        }
    }

    private static void OnParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DynamicsGraphControl control && control._isInitialized)
        {
            control.DrawAll();
        }
    }

    private static void OnInputLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DynamicsGraphControl control && control._isInitialized)
        {
            control.DrawOperatingPoint();
        }
    }

    private static void OnGainReductionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DynamicsGraphControl control && control._isInitialized)
        {
            control.UpdateGainReductionDisplay();
        }
    }

    #endregion

    #region Animation Timer

    private void StartAnimationTimer()
    {
        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void StopAnimationTimer()
    {
        if (_animationTimer != null)
        {
            _animationTimer.Stop();
            _animationTimer.Tick -= OnAnimationTick;
            _animationTimer = null;
        }
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        // Smooth GR meter animation
        double target = GainReduction;
        double diff = target - _displayedGainReduction;
        _displayedGainReduction += diff * 0.3;

        UpdateGainReductionDisplay();
    }

    #endregion

    #region Drawing

    private void DrawAll()
    {
        DrawGrid();
        DrawUnityLine();
        DrawScales();
        DrawKneeRegion();
        DrawThresholdLine();
        DrawTransferCurve();
        DrawOperatingPoint();
        UpdateParameterDisplay();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        double size = Math.Min(GridCanvas.ActualWidth, GridCanvas.ActualHeight);

        if (size <= 0) return;

        // Grid lines every 10 dB
        for (double db = MinDb; db <= MaxDb; db += 10)
        {
            double pos = DbToPosition(db, size);

            // Vertical line (input)
            var vLine = new Shapes.Line
            {
                X1 = pos,
                Y1 = 0,
                X2 = pos,
                Y2 = size,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(vLine);

            // Horizontal line (output)
            var hLine = new Shapes.Line
            {
                X1 = 0,
                Y1 = size - pos,
                X2 = size,
                Y2 = size - pos,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(hLine);
        }
    }

    private void DrawUnityLine()
    {
        UnityLineCanvas.Children.Clear();

        double size = Math.Min(UnityLineCanvas.ActualWidth, UnityLineCanvas.ActualHeight);
        if (size <= 0) return;

        var unityBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4D, 0x52));

        // 1:1 diagonal line
        var line = new Shapes.Line
        {
            X1 = 0,
            Y1 = size,
            X2 = size,
            Y2 = 0,
            Stroke = unityBrush,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        UnityLineCanvas.Children.Add(line);
    }

    private void DrawScales()
    {
        OutputScaleCanvas.Children.Clear();
        InputScaleCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double graphSize = Math.Min(GridCanvas.ActualWidth, GridCanvas.ActualHeight);

        if (graphSize <= 0) return;

        // Output scale (Y axis)
        double[] dbMarks = { 0, -20, -40, -60 };
        foreach (var db in dbMarks)
        {
            double y = graphSize - DbToPosition(db, graphSize);

            var label = new TextBlock
            {
                Text = db == 0 ? "0" : db.ToString(),
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 4);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            OutputScaleCanvas.Children.Add(label);
        }

        // Input scale (X axis)
        foreach (var db in dbMarks)
        {
            double x = DbToPosition(db, graphSize);

            var label = new TextBlock
            {
                Text = db == 0 ? "0" : db.ToString(),
                Foreground = textBrush,
                FontSize = 9
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 4);
            InputScaleCanvas.Children.Add(label);
        }
    }

    private void DrawKneeRegion()
    {
        KneeCanvas.Children.Clear();

        if (KneeWidth <= 0) return;

        double size = Math.Min(KneeCanvas.ActualWidth, KneeCanvas.ActualHeight);
        if (size <= 0) return;

        double kneeStart = Threshold - KneeWidth / 2;
        double kneeEnd = Threshold + KneeWidth / 2;

        double x1 = DbToPosition(kneeStart, size);
        double x2 = DbToPosition(kneeEnd, size);

        var kneeRect = new Shapes.Rectangle
        {
            Width = x2 - x1,
            Height = size,
            Fill = new SolidColorBrush(Color.FromArgb(32, _kneeColor.R, _kneeColor.G, _kneeColor.B))
        };
        Canvas.SetLeft(kneeRect, x1);
        KneeCanvas.Children.Add(kneeRect);
    }

    private void DrawThresholdLine()
    {
        ThresholdCanvas.Children.Clear();

        double size = Math.Min(ThresholdCanvas.ActualWidth, ThresholdCanvas.ActualHeight);
        if (size <= 0) return;

        double thresholdPos = DbToPosition(Threshold, size);

        // Vertical threshold line
        var vLine = new Shapes.Line
        {
            X1 = thresholdPos,
            Y1 = 0,
            X2 = thresholdPos,
            Y2 = size,
            Stroke = new SolidColorBrush(_thresholdColor),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        ThresholdCanvas.Children.Add(vLine);

        // Horizontal threshold line
        var hLine = new Shapes.Line
        {
            X1 = 0,
            Y1 = size - thresholdPos,
            X2 = size,
            Y2 = size - thresholdPos,
            Stroke = new SolidColorBrush(_thresholdColor),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        ThresholdCanvas.Children.Add(hLine);
    }

    private void DrawTransferCurve()
    {
        TransferCurveCanvas.Children.Clear();

        double size = Math.Min(TransferCurveCanvas.ActualWidth, TransferCurveCanvas.ActualHeight);
        if (size <= 0) return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            bool started = false;

            for (int i = 0; i <= CurveResolution; i++)
            {
                double inputDb = MinDb + (MaxDb - MinDb) * i / CurveResolution;
                double outputDb = CalculateOutput(inputDb);

                double x = DbToPosition(inputDb, size);
                double y = size - DbToPosition(outputDb, size);

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
            Stroke = new SolidColorBrush(_transferCurveColor),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        TransferCurveCanvas.Children.Add(path);
    }

    private void DrawOperatingPoint()
    {
        OperatingPointCanvas.Children.Clear();

        if (InputLevel <= MinDb) return;

        double size = Math.Min(OperatingPointCanvas.ActualWidth, OperatingPointCanvas.ActualHeight);
        if (size <= 0) return;

        double outputDb = CalculateOutput(InputLevel);
        double x = DbToPosition(InputLevel, size);
        double y = size - DbToPosition(outputDb, size);

        // Operating point circle
        var point = new Shapes.Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(_operatingPointColor),
            Stroke = Brushes.White,
            StrokeThickness = 1
        };
        Canvas.SetLeft(point, x - 5);
        Canvas.SetTop(point, y - 5);
        OperatingPointCanvas.Children.Add(point);

        // Line from unity to operating point (shows gain reduction)
        double unityY = size - DbToPosition(InputLevel, size);
        if (Math.Abs(y - unityY) > 2)
        {
            var grLine = new Shapes.Line
            {
                X1 = x,
                Y1 = unityY,
                X2 = x,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57)),
                StrokeThickness = 2
            };
            OperatingPointCanvas.Children.Add(grLine);
        }
    }

    private void UpdateParameterDisplay()
    {
        ThresholdText.Text = $"Thr: {Threshold:F0} dB";
        RatioText.Text = Ratio >= 100 ? "Ratio: inf:1" : $"Ratio: {Ratio:F1}:1";
        KneeText.Text = $"Knee: {KneeWidth:F0} dB";
        AttackReleaseText.Text = $"A: {AttackMs:F0}ms R: {ReleaseMs:F0}ms";
    }

    private void UpdateGainReductionDisplay()
    {
        double gr = Math.Min(0, _displayedGainReduction);
        GainReductionText.Text = $"{gr:F1} dB";

        // Color based on amount of reduction
        if (gr < -12)
        {
            GainReductionText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33));
        }
        else if (gr < -6)
        {
            GainReductionText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0x00));
        }
        else
        {
            GainReductionText.Foreground = FindResource("GainReductionBrush") as Brush;
        }
    }

    #endregion

    #region Transfer Function Calculation

    private double CalculateOutput(double inputDb)
    {
        double threshold = Threshold;
        double ratio = Math.Max(1, Ratio);
        double kneeWidth = KneeWidth;
        double makeup = MakeupGain;

        double output;

        if (kneeWidth > 0)
        {
            // Soft knee compression
            double kneeStart = threshold - kneeWidth / 2;
            double kneeEnd = threshold + kneeWidth / 2;

            if (inputDb < kneeStart)
            {
                // Below knee - no compression
                output = inputDb;
            }
            else if (inputDb > kneeEnd)
            {
                // Above knee - full compression
                output = threshold + (inputDb - threshold) / ratio;
            }
            else
            {
                // In knee region - gradual compression
                double kneePos = (inputDb - kneeStart) / kneeWidth;
                double compressionAmount = kneePos * kneePos;
                double effectiveRatio = 1 + (ratio - 1) * compressionAmount;
                output = inputDb - (inputDb - threshold) * (1 - 1 / effectiveRatio) * compressionAmount;
            }
        }
        else
        {
            // Hard knee compression
            if (inputDb < threshold)
            {
                output = inputDb;
            }
            else
            {
                output = threshold + (inputDb - threshold) / ratio;
            }
        }

        return Math.Min(MaxDb, output + makeup);
    }

    #endregion

    #region Coordinate Conversions

    private static double DbToPosition(double db, double size)
    {
        double normalized = (db - MinDb) / (MaxDb - MinDb);
        return normalized * size;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets all compressor parameters at once.
    /// </summary>
    public void SetParameters(double threshold, double ratio, double kneeWidth, double attackMs, double releaseMs, double makeupGain = 0)
    {
        Threshold = threshold;
        Ratio = ratio;
        KneeWidth = kneeWidth;
        AttackMs = attackMs;
        ReleaseMs = releaseMs;
        MakeupGain = makeupGain;
    }

    /// <summary>
    /// Updates the current operating state.
    /// </summary>
    public void SetOperatingState(double inputLevel, double gainReduction)
    {
        InputLevel = inputLevel;
        GainReduction = gainReduction;
    }

    /// <summary>
    /// Resets the display.
    /// </summary>
    public void Reset()
    {
        InputLevel = MinDb;
        GainReduction = 0;
        _displayedGainReduction = 0;
    }

    #endregion
}
