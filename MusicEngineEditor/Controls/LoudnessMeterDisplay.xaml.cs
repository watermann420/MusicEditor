using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// LUFS loudness meter display control for the mixer.
/// Shows integrated, short-term, momentary loudness and true peak.
/// </summary>
public partial class LoudnessMeterDisplay : UserControl
{
    private LoudnessMeter? _loudnessMeter;
    private readonly DispatcherTimer _updateTimer;

    // Meter range constants
    private const double MinLufs = -60.0;
    private const double MaxLufs = 0.0;
    private const double TargetLufs = -14.0; // Streaming target

    // Colors
    private static readonly SolidColorBrush GoodBrush = new(Color.FromRgb(76, 175, 80));    // Green
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(255, 193, 7)); // Yellow
    private static readonly SolidColorBrush DangerBrush = new(Color.FromRgb(244, 67, 54));  // Red

    #region Dependency Properties

    public static readonly DependencyProperty IntegratedLoudnessProperty =
        DependencyProperty.Register(nameof(IntegratedLoudness), typeof(double), typeof(LoudnessMeterDisplay),
            new PropertyMetadata(double.NegativeInfinity, OnLoudnessChanged));

    public static readonly DependencyProperty ShortTermLoudnessProperty =
        DependencyProperty.Register(nameof(ShortTermLoudness), typeof(double), typeof(LoudnessMeterDisplay),
            new PropertyMetadata(double.NegativeInfinity, OnLoudnessChanged));

    public static readonly DependencyProperty MomentaryLoudnessProperty =
        DependencyProperty.Register(nameof(MomentaryLoudness), typeof(double), typeof(LoudnessMeterDisplay),
            new PropertyMetadata(double.NegativeInfinity, OnLoudnessChanged));

    public static readonly DependencyProperty TruePeakProperty =
        DependencyProperty.Register(nameof(TruePeak), typeof(double), typeof(LoudnessMeterDisplay),
            new PropertyMetadata(double.NegativeInfinity, OnLoudnessChanged));

    public static readonly DependencyProperty TargetLoudnessProperty =
        DependencyProperty.Register(nameof(TargetLoudness), typeof(double), typeof(LoudnessMeterDisplay),
            new PropertyMetadata(TargetLufs));

    /// <summary>
    /// Gets or sets the integrated loudness in LUFS.
    /// </summary>
    public double IntegratedLoudness
    {
        get => (double)GetValue(IntegratedLoudnessProperty);
        set => SetValue(IntegratedLoudnessProperty, value);
    }

    /// <summary>
    /// Gets or sets the short-term loudness in LUFS (3 second window).
    /// </summary>
    public double ShortTermLoudness
    {
        get => (double)GetValue(ShortTermLoudnessProperty);
        set => SetValue(ShortTermLoudnessProperty, value);
    }

    /// <summary>
    /// Gets or sets the momentary loudness in LUFS (400ms window).
    /// </summary>
    public double MomentaryLoudness
    {
        get => (double)GetValue(MomentaryLoudnessProperty);
        set => SetValue(MomentaryLoudnessProperty, value);
    }

    /// <summary>
    /// Gets or sets the true peak level in dBTP.
    /// </summary>
    public double TruePeak
    {
        get => (double)GetValue(TruePeakProperty);
        set => SetValue(TruePeakProperty, value);
    }

    /// <summary>
    /// Gets or sets the target loudness for visual reference.
    /// </summary>
    public double TargetLoudness
    {
        get => (double)GetValue(TargetLoudnessProperty);
        set => SetValue(TargetLoudnessProperty, value);
    }

    #endregion

    public LoudnessMeterDisplay()
    {
        InitializeComponent();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // 20 fps update
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDisplay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopMetering();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDisplay();
    }

    private static void OnLoudnessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoudnessMeterDisplay meter)
        {
            meter.UpdateDisplay();
        }
    }

    /// <summary>
    /// Connects the display to a LoudnessMeter for automatic updates.
    /// </summary>
    /// <param name="meter">The loudness meter to connect.</param>
    public void ConnectMeter(LoudnessMeter meter)
    {
        DisconnectMeter();

        _loudnessMeter = meter;
        _loudnessMeter.LoudnessUpdated += LoudnessMeter_LoudnessUpdated;
        _updateTimer.Start();
    }

    /// <summary>
    /// Disconnects from the current loudness meter.
    /// </summary>
    public void DisconnectMeter()
    {
        if (_loudnessMeter != null)
        {
            _loudnessMeter.LoudnessUpdated -= LoudnessMeter_LoudnessUpdated;
            _loudnessMeter = null;
        }
        _updateTimer.Stop();
    }

    /// <summary>
    /// Starts metering (requires a connected meter).
    /// </summary>
    public void StartMetering()
    {
        if (_loudnessMeter != null)
        {
            _updateTimer.Start();
        }
    }

    /// <summary>
    /// Stops metering.
    /// </summary>
    public void StopMetering()
    {
        _updateTimer.Stop();
    }

    /// <summary>
    /// Resets all meter values.
    /// </summary>
    public void Reset()
    {
        IntegratedLoudness = double.NegativeInfinity;
        ShortTermLoudness = double.NegativeInfinity;
        MomentaryLoudness = double.NegativeInfinity;
        TruePeak = double.NegativeInfinity;
        _loudnessMeter?.Reset();
        UpdateDisplay();
    }

    private void LoudnessMeter_LoudnessUpdated(object? sender, LoudnessEventArgs e)
    {
        // Update on UI thread
        Dispatcher.BeginInvoke(() =>
        {
            IntegratedLoudness = e.IntegratedLoudness;
            ShortTermLoudness = e.ShortTermLoudness;
            MomentaryLoudness = e.MomentaryLoudness;
            TruePeak = e.TruePeak;
        });
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_loudnessMeter != null)
        {
            IntegratedLoudness = _loudnessMeter.IntegratedLoudness;
            ShortTermLoudness = _loudnessMeter.ShortTermLoudness;
            MomentaryLoudness = _loudnessMeter.MomentaryLoudness;
            TruePeak = _loudnessMeter.TruePeak;
        }
    }

    private void UpdateDisplay()
    {
        // Update integrated loudness text
        if (double.IsNegativeInfinity(IntegratedLoudness) || double.IsNaN(IntegratedLoudness))
        {
            IntegratedLoudnessText.Text = "--";
            IntegratedLoudnessText.Foreground = new SolidColorBrush(Color.FromRgb(111, 115, 122));
        }
        else
        {
            IntegratedLoudnessText.Text = IntegratedLoudness.ToString("F1");
            IntegratedLoudnessText.Foreground = GetLoudnessColor(IntegratedLoudness);
        }

        // Update short-term text and meter
        UpdateMeterBar(ShortTermMeterFill, ShortTermLoudness);
        ShortTermText.Text = FormatLoudness(ShortTermLoudness);

        // Update momentary text and meter
        UpdateMeterBar(MomentaryMeterFill, MomentaryLoudness);
        MomentaryText.Text = FormatLoudness(MomentaryLoudness);

        // Update true peak
        if (double.IsNegativeInfinity(TruePeak) || double.IsNaN(TruePeak))
        {
            TruePeakText.Text = "-- dB";
            TruePeakText.Foreground = new SolidColorBrush(Color.FromRgb(111, 115, 122));
        }
        else
        {
            TruePeakText.Text = $"{TruePeak:F1} dB";
            TruePeakText.Foreground = TruePeak > -1.0 ? DangerBrush :
                                      TruePeak > -3.0 ? WarningBrush :
                                      new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
        }
    }

    private void UpdateMeterBar(System.Windows.Controls.Border meterFill, double loudness)
    {
        if (meterFill.Parent is not Grid parent)
            return;

        var parentBorder = parent.Parent as System.Windows.Controls.Border;
        if (parentBorder == null)
            return;

        double maxHeight = parentBorder.ActualHeight;
        if (maxHeight <= 0) maxHeight = 80;

        double normalizedLevel = 0;
        if (!double.IsNegativeInfinity(loudness) && !double.IsNaN(loudness))
        {
            normalizedLevel = Math.Clamp((loudness - MinLufs) / (MaxLufs - MinLufs), 0, 1);
        }

        meterFill.Height = normalizedLevel * maxHeight;
        meterFill.Background = GetLoudnessColor(loudness);
    }

    private static Brush GetLoudnessColor(double loudness)
    {
        if (double.IsNegativeInfinity(loudness) || double.IsNaN(loudness))
        {
            return new SolidColorBrush(Color.FromRgb(111, 115, 122));
        }

        if (loudness > -9)
        {
            return DangerBrush; // Too loud
        }
        else if (loudness > -11)
        {
            return WarningBrush; // Warning
        }
        else if (loudness > -18)
        {
            return GoodBrush; // Good range (-14 LUFS target)
        }
        else
        {
            return new SolidColorBrush(Color.FromRgb(111, 115, 122)); // Quiet
        }
    }

    private static string FormatLoudness(double loudness)
    {
        if (double.IsNegativeInfinity(loudness) || double.IsNaN(loudness))
        {
            return "--";
        }
        return loudness.ToString("F0");
    }
}
