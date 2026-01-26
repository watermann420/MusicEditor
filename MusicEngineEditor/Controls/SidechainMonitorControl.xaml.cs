// MusicEngineEditor - Sidechain Monitor Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a sidechain source option.
/// </summary>
public class SidechainSource
{
    /// <summary>
    /// Gets or sets the source ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source color.
    /// </summary>
    public Color Color { get; set; } = Colors.Gray;

    public override string ToString() => Name;
}

/// <summary>
/// A sidechain monitor control for previewing sidechain source signals.
/// Features source selector, level meter, solo sidechain option, and filter preview.
/// </summary>
public partial class SidechainMonitorControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty SidechainLevelProperty =
        DependencyProperty.Register(nameof(SidechainLevel), typeof(float), typeof(SidechainMonitorControl),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty IsSoloEnabledProperty =
        DependencyProperty.Register(nameof(IsSoloEnabled), typeof(bool), typeof(SidechainMonitorControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty HighPassFrequencyProperty =
        DependencyProperty.Register(nameof(HighPassFrequency), typeof(double), typeof(SidechainMonitorControl),
            new PropertyMetadata(20.0));

    public static readonly DependencyProperty LowPassFrequencyProperty =
        DependencyProperty.Register(nameof(LowPassFrequency), typeof(double), typeof(SidechainMonitorControl),
            new PropertyMetadata(20000.0));

    public static readonly DependencyProperty ListenGainProperty =
        DependencyProperty.Register(nameof(ListenGain), typeof(double), typeof(SidechainMonitorControl),
            new PropertyMetadata(0.0));

    /// <summary>
    /// Gets or sets the current sidechain signal level (linear 0-1+).
    /// </summary>
    public float SidechainLevel
    {
        get => (float)GetValue(SidechainLevelProperty);
        set => SetValue(SidechainLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets whether solo sidechain is enabled.
    /// </summary>
    public bool IsSoloEnabled
    {
        get => (bool)GetValue(IsSoloEnabledProperty);
        set => SetValue(IsSoloEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the high-pass filter frequency in Hz.
    /// </summary>
    public double HighPassFrequency
    {
        get => (double)GetValue(HighPassFrequencyProperty);
        set => SetValue(HighPassFrequencyProperty, value);
    }

    /// <summary>
    /// Gets or sets the low-pass filter frequency in Hz.
    /// </summary>
    public double LowPassFrequency
    {
        get => (double)GetValue(LowPassFrequencyProperty);
        set => SetValue(LowPassFrequencyProperty, value);
    }

    /// <summary>
    /// Gets or sets the listen gain in dB.
    /// </summary>
    public double ListenGain
    {
        get => (double)GetValue(ListenGainProperty);
        set => SetValue(ListenGainProperty, value);
    }

    #endregion

    #region Private Fields

    private readonly ObservableCollection<SidechainSource> _sources = new();
    private SidechainSource? _selectedSource;
    private double _displayedLevel;
    private double _peakLevel;
    private DateTime _peakHoldTime;
    private bool _isInitialized;

    // Spectrum data for visualization
    private readonly double[] _spectrumData = new double[32];
    private readonly Random _random = new(); // For demo visualization

    private DispatcherTimer? _updateTimer;
    private DateTime _lastUpdateTime;

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the sidechain source is changed.
    /// </summary>
    public event EventHandler<SidechainSource?>? SourceChanged;

    /// <summary>
    /// Event raised when solo sidechain state changes.
    /// </summary>
    public event EventHandler<bool>? SoloChanged;

    /// <summary>
    /// Event raised when filter settings change.
    /// </summary>
    public event EventHandler? FilterChanged;

    #endregion

    #region Constructor

    public SidechainMonitorControl()
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
        StartUpdateTimer();
        UpdateFilterDisplay();
        DrawFilterCurve();
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
            DrawFilterCurve();
            DrawSpectrum();
        }
    }

    private void OnSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceSelector.SelectedItem is SidechainSource source)
        {
            _selectedSource = source;
            bool isActive = source.Id != "none";

            ActiveIndicator.Opacity = isActive ? 1.0 : 0.3;
            ActiveText.Text = isActive ? "Active" : "Inactive";

            SourceChanged?.Invoke(this, isActive ? source : null);
        }
        else
        {
            _selectedSource = null;
            ActiveIndicator.Opacity = 0.3;
            ActiveText.Text = "Inactive";
            SourceChanged?.Invoke(this, null);
        }
    }

    private void OnFilterChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        HighPassFrequency = HpfSlider.Value;
        LowPassFrequency = LpfSlider.Value;

        UpdateFilterDisplay();
        DrawFilterCurve();

        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnListenGainChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ListenGain = ListenGainSlider.Value;
        ListenGainText.Text = $"{ListenGain:F0} dB";
    }

    private void OnSoloChanged(object sender, RoutedEventArgs e)
    {
        IsSoloEnabled = SoloSidechainButton.IsChecked == true;
        SoloChanged?.Invoke(this, IsSoloEnabled);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears all sidechain sources.
    /// </summary>
    public void ClearSources()
    {
        _sources.Clear();
        SourceSelector.Items.Clear();
        SourceSelector.Items.Add(new ComboBoxItem { Content = "None", Tag = new SidechainSource { Id = "none", Name = "None" } });
        SourceSelector.SelectedIndex = 0;
    }

    /// <summary>
    /// Adds a sidechain source option.
    /// </summary>
    public void AddSource(string id, string name, Color color)
    {
        var source = new SidechainSource { Id = id, Name = name, Color = color };
        _sources.Add(source);
        SourceSelector.Items.Add(new ComboBoxItem { Content = name, Tag = source });
    }

    /// <summary>
    /// Sets the sidechain signal level.
    /// </summary>
    public void SetLevel(float level)
    {
        SidechainLevel = level;
    }

    /// <summary>
    /// Sets the spectrum data for visualization.
    /// </summary>
    /// <param name="data">Array of 32 frequency band levels (0-1).</param>
    public void SetSpectrumData(double[] data)
    {
        if (data == null || data.Length != _spectrumData.Length)
            return;

        Array.Copy(data, _spectrumData, _spectrumData.Length);
    }

    /// <summary>
    /// Resets the meter and peak hold.
    /// </summary>
    public void Reset()
    {
        SidechainLevel = 0f;
        _displayedLevel = 0;
        _peakLevel = 0;
        Array.Clear(_spectrumData, 0, _spectrumData.Length);
        UpdateVisuals();
    }

    #endregion

    #region Private Methods - Visual Updates

    private void UpdateFilterDisplay()
    {
        HpfValueText.Text = FormatFrequency(HighPassFrequency);
        LpfValueText.Text = FormatFrequency(LowPassFrequency);
    }

    private void DrawFilterCurve()
    {
        if (SpectrumCanvas.ActualWidth <= 0 || SpectrumCanvas.ActualHeight <= 0)
            return;

        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;

        // Create filter response curve
        var points = new PointCollection();
        int numPoints = 64;

        for (int i = 0; i < numPoints; i++)
        {
            double freq = FrequencyFromX(i / (double)(numPoints - 1), width);
            double response = CalculateFilterResponse(freq);
            double y = height * (1 - response);
            double x = i / (double)(numPoints - 1) * width;
            points.Add(new Point(x, y));
        }

        // Create path geometry from points
        if (points.Count > 1)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = points[0] };

            for (int i = 1; i < points.Count; i++)
            {
                figure.Segments.Add(new LineSegment(points[i], true));
            }

            geometry.Figures.Add(figure);
            FilterCurve.Data = geometry;
        }
    }

    private void DrawSpectrum()
    {
        SpectrumCanvas.Children.Clear();

        if (SpectrumCanvas.ActualWidth <= 0 || SpectrumCanvas.ActualHeight <= 0)
            return;

        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;
        double barWidth = width / _spectrumData.Length - 1;

        var brush = new SolidColorBrush(Color.FromArgb(100, 0xFF, 0x95, 0x00));

        for (int i = 0; i < _spectrumData.Length; i++)
        {
            double barHeight = height * _spectrumData[i];
            var rect = new Shapes.Rectangle
            {
                Width = Math.Max(1, barWidth),
                Height = barHeight,
                Fill = brush
            };

            Canvas.SetLeft(rect, i * (barWidth + 1));
            Canvas.SetTop(rect, height - barHeight);
            SpectrumCanvas.Children.Add(rect);
        }
    }

    private void UpdateMeter()
    {
        if (LevelBar.Parent is not Grid parent)
            return;

        double availableHeight = parent.ActualHeight - 4;
        if (availableHeight <= 0)
            return;

        // Convert to dB and normalize
        double levelDb = SidechainLevel <= 0 ? -60 : 20 * Math.Log10(SidechainLevel);
        levelDb = Math.Max(-60, Math.Min(0, levelDb));
        double normalized = (levelDb + 60) / 60;

        // Smooth display
        double targetLevel = normalized;
        double alpha = targetLevel > _displayedLevel ? 0.8 : 0.2;
        _displayedLevel += alpha * (targetLevel - _displayedLevel);

        LevelBar.Height = availableHeight * _displayedLevel;

        // Peak hold
        var now = DateTime.UtcNow;
        if (_displayedLevel > _peakLevel)
        {
            _peakLevel = _displayedLevel;
            _peakHoldTime = now;
        }
        else if ((now - _peakHoldTime).TotalSeconds > 1.5)
        {
            _peakLevel = Math.Max(0, _peakLevel - 0.02);
        }

        PeakHoldLine.Margin = new Thickness(3, 0, 3, availableHeight * _peakLevel);

        // Update level text
        LevelValueText.Text = levelDb <= -60 ? "-inf dB" : $"{levelDb:F1} dB";
    }

    private void UpdateVisuals()
    {
        UpdateMeter();
        DrawSpectrum();
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
        // Generate demo spectrum data if active
        if (_selectedSource != null && _selectedSource.Id != "none")
        {
            for (int i = 0; i < _spectrumData.Length; i++)
            {
                double freq = 20 * Math.Pow(1000, i / (double)(_spectrumData.Length - 1));
                double response = CalculateFilterResponse(freq);
                _spectrumData[i] = _spectrumData[i] * 0.7 + (_random.NextDouble() * 0.3 + 0.2) * response * 0.3;
            }
        }
        else
        {
            for (int i = 0; i < _spectrumData.Length; i++)
            {
                _spectrumData[i] *= 0.9;
            }
        }

        UpdateVisuals();
    }

    #endregion

    #region Private Methods - Utility

    private static string FormatFrequency(double freq)
    {
        if (freq >= 1000)
            return $"{freq / 1000:F1} kHz";
        return $"{freq:F0} Hz";
    }

    private double CalculateFilterResponse(double freq)
    {
        // Simple high-pass and low-pass response calculation
        double hpfResponse = 1.0;
        double lpfResponse = 1.0;

        if (freq < HighPassFrequency)
        {
            double ratio = freq / HighPassFrequency;
            hpfResponse = ratio * ratio; // 12 dB/octave approximation
        }

        if (freq > LowPassFrequency)
        {
            double ratio = LowPassFrequency / freq;
            lpfResponse = ratio * ratio;
        }

        return Math.Min(hpfResponse, lpfResponse);
    }

    private static double FrequencyFromX(double x, double width)
    {
        // Logarithmic frequency scale: 20 Hz to 20 kHz
        return 20 * Math.Pow(1000, x);
    }

    #endregion
}
