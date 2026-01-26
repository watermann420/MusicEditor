// MusicEngineEditor - Gain Reduction Meter Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A gain reduction meter for compressor/limiter visualization.
/// Shows the amount of gain reduction with peak hold and attack/release visualization.
/// </summary>
public partial class GainReductionMeter : UserControl
{
    #region Constants

    private const double MinGR = 0.0;      // 0 dB GR
    private const double MaxGR = 30.0;     // 30 dB GR (max display)
    private const double GRRange = MaxGR - MinGR;
    private const double PeakHoldTime = 2.0; // seconds
    private const double PeakFallRate = 15.0; // dB/s

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty GainReductionProperty =
        DependencyProperty.Register(nameof(GainReduction), typeof(float), typeof(GainReductionMeter),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty ThresholdDbProperty =
        DependencyProperty.Register(nameof(ThresholdDb), typeof(float), typeof(GainReductionMeter),
            new PropertyMetadata(-20f, OnThresholdChanged));

    public static readonly DependencyProperty ShowThresholdProperty =
        DependencyProperty.Register(nameof(ShowThreshold), typeof(bool), typeof(GainReductionMeter),
            new PropertyMetadata(false, OnShowThresholdChanged));

    public static readonly DependencyProperty AttackMsProperty =
        DependencyProperty.Register(nameof(AttackMs), typeof(float), typeof(GainReductionMeter),
            new PropertyMetadata(10f));

    public static readonly DependencyProperty ReleaseMsProperty =
        DependencyProperty.Register(nameof(ReleaseMs), typeof(float), typeof(GainReductionMeter),
            new PropertyMetadata(100f));

    /// <summary>
    /// Gets or sets the current gain reduction in dB (positive value = reduction).
    /// </summary>
    public float GainReduction
    {
        get => (float)GetValue(GainReductionProperty);
        set => SetValue(GainReductionProperty, value);
    }

    /// <summary>
    /// Gets or sets the compressor threshold in dBFS.
    /// </summary>
    public float ThresholdDb
    {
        get => (float)GetValue(ThresholdDbProperty);
        set => SetValue(ThresholdDbProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the threshold line on the meter.
    /// </summary>
    public bool ShowThreshold
    {
        get => (bool)GetValue(ShowThresholdProperty);
        set => SetValue(ShowThresholdProperty, value);
    }

    /// <summary>
    /// Gets or sets the compressor attack time in milliseconds.
    /// </summary>
    public float AttackMs
    {
        get => (float)GetValue(AttackMsProperty);
        set => SetValue(AttackMsProperty, value);
    }

    /// <summary>
    /// Gets or sets the compressor release time in milliseconds.
    /// </summary>
    public float ReleaseMs
    {
        get => (float)GetValue(ReleaseMsProperty);
        set => SetValue(ReleaseMsProperty, value);
    }

    #endregion

    #region Private Fields

    private double _displayedGR;
    private double _peakGR;
    private DateTime _peakHoldTime;
    private bool _isInitialized;

    private DispatcherTimer? _updateTimer;
    private DateTime _lastUpdateTime;

    // History for attack/release visualization
    private readonly double[] _grHistory = new double[30];
    private int _historyIndex;

    #endregion

    #region Constructor

    public GainReductionMeter()
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
        DrawScale();
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
            UpdateVisuals();
        }
    }

    private static void OnThresholdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Threshold visualization update handled in UpdateVisuals
    }

    private static void OnShowThresholdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GainReductionMeter meter && meter._isInitialized)
        {
            meter.ThresholdLine.Visibility = meter.ShowThreshold ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnPeakReset(object sender, MouseButtonEventArgs e)
    {
        ResetPeak();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the gain reduction value.
    /// </summary>
    /// <param name="grDb">Gain reduction in dB (positive = compression).</param>
    public void SetGainReduction(float grDb)
    {
        GainReduction = grDb;
    }

    /// <summary>
    /// Resets the meter and peak hold.
    /// </summary>
    public void Reset()
    {
        GainReduction = 0f;
        _displayedGR = 0;
        _peakGR = 0;
        Array.Clear(_grHistory, 0, _grHistory.Length);
        UpdateVisuals();
    }

    /// <summary>
    /// Resets only the peak hold value.
    /// </summary>
    public void ResetPeak()
    {
        _peakGR = 0;
        UpdatePeakDisplay();
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

        // GR scale marks (dB of gain reduction)
        double[] grMarks = { 0, 3, 6, 10, 15, 20, 30 };

        foreach (double gr in grMarks)
        {
            double normalized = gr / MaxGR;
            double y = height * normalized;

            // Tick line
            var tick = new Shapes.Line
            {
                X1 = 12,
                Y1 = y,
                X2 = 16,
                Y2 = y,
                Stroke = lineBrush,
                StrokeThickness = 1
            };
            ScaleCanvas.Children.Add(tick);

            // Label
            var label = new TextBlock
            {
                Text = gr.ToString(),
                FontSize = 8,
                Foreground = textBrush
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            ScaleCanvas.Children.Add(label);
        }
    }

    private void DrawDynamicsVisualization()
    {
        DynamicsCanvas.Children.Clear();

        if (DynamicsCanvas.ActualWidth <= 0 || DynamicsCanvas.ActualHeight <= 0)
            return;

        double width = DynamicsCanvas.ActualWidth;
        double height = DynamicsCanvas.ActualHeight;

        // Draw GR history as a mini waveform
        var points = new PointCollection();
        for (int i = 0; i < _grHistory.Length; i++)
        {
            int idx = (_historyIndex + i) % _grHistory.Length;
            double x = (double)i / (_grHistory.Length - 1) * width;
            double y = (_grHistory[idx] / MaxGR) * height;
            points.Add(new Point(x, y));
        }

        if (points.Count > 1)
        {
            var polyline = new Shapes.Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 140, 0)),
                StrokeThickness = 1,
                Fill = null
            };
            DynamicsCanvas.Children.Add(polyline);
        }
    }

    #endregion

    #region Private Methods - Level Processing

    private void ProcessGainReduction(double deltaTime)
    {
        double targetGR = Math.Max(0, Math.Min(MaxGR, GainReduction));

        // Simulate attack/release behavior in display
        double attackCoeff = 1.0 - Math.Exp(-deltaTime / (AttackMs / 1000.0));
        double releaseCoeff = 1.0 - Math.Exp(-deltaTime / (ReleaseMs / 1000.0));

        if (targetGR > _displayedGR)
        {
            // Attack: fast response
            _displayedGR += attackCoeff * (targetGR - _displayedGR);
        }
        else
        {
            // Release: slower response
            _displayedGR += releaseCoeff * (targetGR - _displayedGR);
        }

        // Update peak hold
        var now = DateTime.UtcNow;
        if (_displayedGR > _peakGR)
        {
            _peakGR = _displayedGR;
            _peakHoldTime = now;
        }
        else if ((now - _peakHoldTime).TotalSeconds > PeakHoldTime)
        {
            _peakGR = Math.Max(0, _peakGR - PeakFallRate * deltaTime);
        }

        // Update history for visualization
        _grHistory[_historyIndex] = _displayedGR;
        _historyIndex = (_historyIndex + 1) % _grHistory.Length;
    }

    #endregion

    #region Private Methods - Visual Updates

    private void UpdateVisuals()
    {
        UpdateGRBar();
        UpdatePeakHold();
        UpdatePeakDisplay();
        DrawDynamicsVisualization();
    }

    private void UpdateGRBar()
    {
        if (GRBar.Parent is not Grid parent)
            return;

        double availableHeight = parent.ActualHeight - 4;
        if (availableHeight <= 0)
            return;

        double normalized = _displayedGR / MaxGR;
        normalized = Math.Max(0, Math.Min(1, normalized));

        GRBar.Height = availableHeight * normalized;

        // Add glow effect when actively compressing
        if (_displayedGR > 1)
        {
            GRBar.Effect = FindResource("GRGlowEffect") as System.Windows.Media.Effects.Effect;
        }
        else
        {
            GRBar.Effect = null;
        }
    }

    private void UpdatePeakHold()
    {
        if (PeakHoldLine.Parent is not Grid parent)
            return;

        double availableHeight = parent.ActualHeight - 4;
        if (availableHeight <= 0)
            return;

        double normalized = _peakGR / MaxGR;
        normalized = Math.Max(0, Math.Min(1, normalized));

        double topMargin = availableHeight * normalized;
        PeakHoldLine.Margin = new Thickness(3, topMargin, 3, 0);
    }

    private void UpdatePeakDisplay()
    {
        GRValueText.Text = _displayedGR < 0.1 ? "0.0 dB" : $"-{_displayedGR:F1} dB";
        PeakGRText.Text = _peakGR < 0.1 ? "0.0" : $"-{_peakGR:F1}";

        // Color code based on amount of GR
        if (_displayedGR > 15)
        {
            GRValueText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x00));
        }
        else if (_displayedGR > 6)
        {
            GRValueText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00));
        }
        else
        {
            GRValueText.Foreground = FindResource("GRBrush") as Brush;
        }
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

        ProcessGainReduction(deltaTime);
        UpdateVisuals();
    }

    #endregion
}
