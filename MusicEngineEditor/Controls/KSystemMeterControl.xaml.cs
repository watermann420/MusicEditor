// MusicEngineEditor - K-System Meter Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// K-System metering mode (Bob Katz standard).
/// </summary>
public enum KSystemMode
{
    /// <summary>K-12 for broadcast (0 VU = -12 dBFS).</summary>
    K12,
    /// <summary>K-14 for pop/rock (0 VU = -14 dBFS).</summary>
    K14,
    /// <summary>K-20 for classical/film (0 VU = -20 dBFS).</summary>
    K20
}

/// <summary>
/// A K-System meter control implementing Bob Katz's metering standard.
/// Provides K-12, K-14, and K-20 scales with color-coded zones.
/// </summary>
public partial class KSystemMeterControl : UserControl
{
    #region Constants

    private const double MinDb = -40.0;
    private const double MaxDb = 0.0;
    private const double DbRange = MaxDb - MinDb;

    // Peak hold and ballistics
    private const double PeakHoldTime = 1.5;
    private const double PeakFallRate = 20.0; // dB/s
    private const double AverageIntegrationTime = 0.4; // 400ms RMS integration

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty LeftLevelProperty =
        DependencyProperty.Register(nameof(LeftLevel), typeof(float), typeof(KSystemMeterControl),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty RightLevelProperty =
        DependencyProperty.Register(nameof(RightLevel), typeof(float), typeof(KSystemMeterControl),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty LeftAverageLevelProperty =
        DependencyProperty.Register(nameof(LeftAverageLevel), typeof(float), typeof(KSystemMeterControl),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty RightAverageLevelProperty =
        DependencyProperty.Register(nameof(RightAverageLevel), typeof(float), typeof(KSystemMeterControl),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty KModeProperty =
        DependencyProperty.Register(nameof(KMode), typeof(KSystemMode), typeof(KSystemMeterControl),
            new PropertyMetadata(KSystemMode.K14, OnKModePropertyChanged));

    /// <summary>
    /// Gets or sets the left channel peak level (linear 0-1+).
    /// </summary>
    public float LeftLevel
    {
        get => (float)GetValue(LeftLevelProperty);
        set => SetValue(LeftLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the right channel peak level (linear 0-1+).
    /// </summary>
    public float RightLevel
    {
        get => (float)GetValue(RightLevelProperty);
        set => SetValue(RightLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the left channel average (RMS) level (linear 0-1+).
    /// </summary>
    public float LeftAverageLevel
    {
        get => (float)GetValue(LeftAverageLevelProperty);
        set => SetValue(LeftAverageLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the right channel average (RMS) level (linear 0-1+).
    /// </summary>
    public float RightAverageLevel
    {
        get => (float)GetValue(RightAverageLevelProperty);
        set => SetValue(RightAverageLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the K-System mode (K-12, K-14, or K-20).
    /// </summary>
    public KSystemMode KMode
    {
        get => (KSystemMode)GetValue(KModeProperty);
        set => SetValue(KModeProperty, value);
    }

    #endregion

    #region Private Fields

    private double _displayedLeftPeak;
    private double _displayedRightPeak;
    private double _displayedLeftAvg;
    private double _displayedRightAvg;
    private double _leftPeakHold;
    private double _rightPeakHold;
    private DateTime _leftPeakHoldTime;
    private DateTime _rightPeakHoldTime;

    private DispatcherTimer? _updateTimer;
    private DateTime _lastUpdateTime;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public KSystemMeterControl()
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
        UpdateKModeDisplay();
        DrawScale();
        DrawZoneBackgrounds();
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
            DrawScale();
            DrawZoneBackgrounds();
            UpdateVisuals();
        }
    }

    private void OnKModeChanged(object sender, RoutedEventArgs e)
    {
        if (K12Button.IsChecked == true)
            KMode = KSystemMode.K12;
        else if (K14Button.IsChecked == true)
            KMode = KSystemMode.K14;
        else if (K20Button.IsChecked == true)
            KMode = KSystemMode.K20;
    }

    private static void OnKModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KSystemMeterControl meter && meter._isInitialized)
        {
            meter.UpdateKModeDisplay();
            meter.DrawScale();
            meter.DrawZoneBackgrounds();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets both peak and average levels for both channels.
    /// </summary>
    public void SetLevels(float leftPeak, float rightPeak, float leftAvg, float rightAvg)
    {
        LeftLevel = leftPeak;
        RightLevel = rightPeak;
        LeftAverageLevel = leftAvg;
        RightAverageLevel = rightAvg;
    }

    /// <summary>
    /// Resets the meter to zero.
    /// </summary>
    public void Reset()
    {
        LeftLevel = 0f;
        RightLevel = 0f;
        LeftAverageLevel = 0f;
        RightAverageLevel = 0f;
        _displayedLeftPeak = MinDb;
        _displayedRightPeak = MinDb;
        _displayedLeftAvg = MinDb;
        _displayedRightAvg = MinDb;
        _leftPeakHold = MinDb;
        _rightPeakHold = MinDb;
        UpdateVisuals();
    }

    #endregion

    #region Private Methods - Drawing

    private void DrawScale()
    {
        ScaleCanvas.Children.Clear();

        if (ScaleCanvas.ActualHeight <= 0)
            return;

        double height = ScaleCanvas.ActualHeight;
        var textBrush = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
        var lineBrush = FindResource("MeterBorderBrush") as Brush ?? Brushes.DarkGray;

        // K-System specific scale marks
        double headroom = GetHeadroom();
        double[] dbMarks = { 0, -3, -6, -10, -14, -20, -30, -40 };

        foreach (double db in dbMarks)
        {
            // Adjust for K-System: the scale shows "K" values where 0K = -headroom dBFS
            double kValue = db + headroom;
            double normalized = (db - MinDb) / DbRange;
            double y = height * (1 - normalized);

            // Tick line
            var tick = new Shapes.Line
            {
                X1 = 14,
                Y1 = y,
                X2 = 18,
                Y2 = y,
                Stroke = lineBrush,
                StrokeThickness = 1
            };
            ScaleCanvas.Children.Add(tick);

            // Label (show K value for key positions)
            if (Math.Abs(db) < 0.1 || Math.Abs(db + headroom) < 0.1 || db == -20 || db == -40)
            {
                string labelText;
                if (Math.Abs(db) < 0.1)
                    labelText = "0";
                else if (Math.Abs(db + headroom) < 0.1)
                    labelText = "0K";
                else
                    labelText = db.ToString();

                var label = new TextBlock
                {
                    Text = labelText,
                    FontSize = 8,
                    Foreground = textBrush
                };

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetRight(label, 20 - label.DesiredSize.Width);
                Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
                ScaleCanvas.Children.Add(label);
            }
        }
    }

    private void DrawZoneBackgrounds()
    {
        DrawZoneCanvas(LeftZoneCanvas);
        DrawZoneCanvas(RightZoneCanvas);
    }

    private void DrawZoneCanvas(Canvas canvas)
    {
        canvas.Children.Clear();

        if (canvas.ActualHeight <= 0 || canvas.ActualWidth <= 0)
            return;

        double height = canvas.ActualHeight;
        double width = canvas.ActualWidth;
        double headroom = GetHeadroom();

        // Red zone: 0 dBFS to -headroom + 4 dB (last 4 dB before digital ceiling)
        double redTop = 0;
        double redBottom = DbToY(-4, height);
        AddZoneRect(canvas, "#330000", redTop, redBottom, width);

        // Yellow zone: -4 dB to 0K (which is -headroom dBFS)
        double yellowTop = redBottom;
        double yellowBottom = DbToY(-headroom, height);
        AddZoneRect(canvas, "#333300", yellowTop, yellowBottom, width);

        // Green zone: 0K to bottom
        double greenTop = yellowBottom;
        double greenBottom = height;
        AddZoneRect(canvas, "#003300", greenTop, greenBottom, width);
    }

    private static void AddZoneRect(Canvas canvas, string colorHex, double top, double bottom, double width)
    {
        var rect = new Shapes.Rectangle
        {
            Width = width,
            Height = Math.Max(0, bottom - top),
            Fill = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex))
        };
        Canvas.SetTop(rect, top);
        canvas.Children.Add(rect);
    }

    #endregion

    #region Private Methods - Level Processing

    private void ProcessLevels(double deltaTime)
    {
        // Convert linear to dB
        double leftPeakDb = LinearToDb(LeftLevel);
        double rightPeakDb = LinearToDb(RightLevel);
        double leftAvgDb = LinearToDb(LeftAverageLevel);
        double rightAvgDb = LinearToDb(RightAverageLevel);

        // Instant attack, slow release for peak
        double releaseAmount = PeakFallRate * deltaTime;

        if (leftPeakDb > _displayedLeftPeak)
            _displayedLeftPeak = leftPeakDb;
        else
            _displayedLeftPeak = Math.Max(MinDb, _displayedLeftPeak - releaseAmount);

        if (rightPeakDb > _displayedRightPeak)
            _displayedRightPeak = rightPeakDb;
        else
            _displayedRightPeak = Math.Max(MinDb, _displayedRightPeak - releaseAmount);

        // Smooth averaging for RMS display
        double avgAlpha = 1.0 - Math.Exp(-deltaTime / AverageIntegrationTime);
        _displayedLeftAvg += avgAlpha * (leftAvgDb - _displayedLeftAvg);
        _displayedRightAvg += avgAlpha * (rightAvgDb - _displayedRightAvg);

        // Peak hold
        var now = DateTime.UtcNow;

        if (_displayedLeftPeak > _leftPeakHold)
        {
            _leftPeakHold = _displayedLeftPeak;
            _leftPeakHoldTime = now;
        }
        else if ((now - _leftPeakHoldTime).TotalSeconds > PeakHoldTime)
        {
            _leftPeakHold = Math.Max(MinDb, _leftPeakHold - releaseAmount);
        }

        if (_displayedRightPeak > _rightPeakHold)
        {
            _rightPeakHold = _displayedRightPeak;
            _rightPeakHoldTime = now;
        }
        else if ((now - _rightPeakHoldTime).TotalSeconds > PeakHoldTime)
        {
            _rightPeakHold = Math.Max(MinDb, _rightPeakHold - releaseAmount);
        }
    }

    #endregion

    #region Private Methods - Visual Updates

    private void UpdateVisuals()
    {
        UpdateMeterBars();
        UpdatePeakHolds();
        UpdateTextDisplays();
    }

    private void UpdateMeterBars()
    {
        if (LeftPeakBar.Parent is not Grid leftParent || RightPeakBar.Parent is not Grid rightParent)
            return;

        double leftHeight = leftParent.ActualHeight - 4;
        double rightHeight = rightParent.ActualHeight - 4;

        if (leftHeight <= 0 || rightHeight <= 0)
            return;

        // Peak bars
        double leftPeakNorm = DbToNormalized(_displayedLeftPeak);
        double rightPeakNorm = DbToNormalized(_displayedRightPeak);

        LeftPeakBar.Height = leftHeight * leftPeakNorm;
        RightPeakBar.Height = rightHeight * rightPeakNorm;

        // Color bars based on level
        LeftPeakBar.Background = GetLevelBrush(_displayedLeftPeak);
        RightPeakBar.Background = GetLevelBrush(_displayedRightPeak);

        // Average bars
        double leftAvgNorm = DbToNormalized(_displayedLeftAvg);
        double rightAvgNorm = DbToNormalized(_displayedRightAvg);

        LeftAverageBar.Height = leftHeight * leftAvgNorm;
        RightAverageBar.Height = rightHeight * rightAvgNorm;

        LeftAverageBar.Background = GetLevelBrush(_displayedLeftAvg);
        RightAverageBar.Background = GetLevelBrush(_displayedRightAvg);
    }

    private void UpdatePeakHolds()
    {
        if (LeftPeakHold.Parent is not Grid leftParent || RightPeakHold.Parent is not Grid rightParent)
            return;

        double leftHeight = leftParent.ActualHeight - 4;
        double rightHeight = rightParent.ActualHeight - 4;

        if (leftHeight <= 0 || rightHeight <= 0)
            return;

        double leftHoldNorm = DbToNormalized(_leftPeakHold);
        double rightHoldNorm = DbToNormalized(_rightPeakHold);

        LeftPeakHold.Margin = new Thickness(3, 0, 3, leftHeight * leftHoldNorm);
        RightPeakHold.Margin = new Thickness(3, 0, 3, rightHeight * rightHoldNorm);
    }

    private void UpdateTextDisplays()
    {
        double maxPeak = Math.Max(_leftPeakHold, _rightPeakHold);
        double avgLevel = (_displayedLeftAvg + _displayedRightAvg) / 2;

        PeakText.Text = maxPeak <= MinDb ? "-inf" : $"{maxPeak:F1}";
        AverageText.Text = avgLevel <= MinDb ? "-inf" : $"{avgLevel:F1}";

        // Color code peak text
        double headroom = GetHeadroom();
        if (maxPeak > -4)
            PeakText.Foreground = FindResource("KRedBrush") as Brush;
        else if (maxPeak > -headroom)
            PeakText.Foreground = FindResource("KYellowBrush") as Brush;
        else
            PeakText.Foreground = FindResource("TextPrimaryBrush") as Brush;
    }

    private void UpdateKModeDisplay()
    {
        // Update radio buttons
        K12Button.IsChecked = KMode == KSystemMode.K12;
        K14Button.IsChecked = KMode == KSystemMode.K14;
        K20Button.IsChecked = KMode == KSystemMode.K20;

        // Update reference text
        double headroom = GetHeadroom();
        ReferenceLevelText.Text = $"0 VU = -{headroom:F0} dBFS";
    }

    private Brush GetLevelBrush(double db)
    {
        double headroom = GetHeadroom();

        if (db > -4)
            return FindResource("KRedBrush") as Brush ?? Brushes.Red;
        else if (db > -headroom)
            return FindResource("KYellowBrush") as Brush ?? Brushes.Yellow;
        else
            return FindResource("KGreenBrush") as Brush ?? Brushes.Green;
    }

    #endregion

    #region Private Methods - Timer

    private void StartUpdateTimer()
    {
        _lastUpdateTime = DateTime.UtcNow;
        _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
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

        ProcessLevels(deltaTime);
        UpdateVisuals();
    }

    #endregion

    #region Private Methods - Utility

    private double GetHeadroom()
    {
        return KMode switch
        {
            KSystemMode.K12 => 12.0,
            KSystemMode.K14 => 14.0,
            KSystemMode.K20 => 20.0,
            _ => 14.0
        };
    }

    private static double LinearToDb(float linear)
    {
        if (linear <= 0)
            return MinDb;
        return Math.Max(MinDb, 20.0 * Math.Log10(linear));
    }

    private static double DbToNormalized(double db)
    {
        return Math.Max(0, Math.Min(1, (db - MinDb) / DbRange));
    }

    private static double DbToY(double db, double height)
    {
        double normalized = DbToNormalized(db);
        return height * (1 - normalized);
    }

    #endregion
}
