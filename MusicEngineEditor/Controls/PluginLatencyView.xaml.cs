// MusicEngineEditor - Plugin Latency View Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a latency entry for a track or plugin.
/// </summary>
public class LatencyEntry : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private int _samples;
    private double _milliseconds;
    private Brush _color = Brushes.Gray;
    private Brush _barColor = Brushes.Blue;
    private double _barWidth;
    private bool _isPlugin;
    private int _indent;

    /// <summary>
    /// Gets or sets the track or plugin name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the latency in samples.
    /// </summary>
    public int Samples
    {
        get => _samples;
        set { _samples = value; OnPropertyChanged(); OnPropertyChanged(nameof(SamplesText)); }
    }

    /// <summary>
    /// Gets or sets the latency in milliseconds.
    /// </summary>
    public double Milliseconds
    {
        get => _milliseconds;
        set { _milliseconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(MsText)); }
    }

    /// <summary>
    /// Gets or sets the color indicator for the entry.
    /// </summary>
    public Brush Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the bar color for the latency visualization.
    /// </summary>
    public Brush BarColor
    {
        get => _barColor;
        set { _barColor = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the bar width for visualization.
    /// </summary>
    public double BarWidth
    {
        get => _barWidth;
        set { _barWidth = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets whether this entry represents a plugin (vs a track).
    /// </summary>
    public bool IsPlugin
    {
        get => _isPlugin;
        set { _isPlugin = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the indentation level for hierarchy display.
    /// </summary>
    public int Indent
    {
        get => _indent;
        set { _indent = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets the formatted samples text.
    /// </summary>
    public string SamplesText => Samples.ToString("N0");

    /// <summary>
    /// Gets the formatted milliseconds text.
    /// </summary>
    public string MsText => $"{Milliseconds:F2}";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// A visual control for displaying plugin latency information per track.
/// Shows per-track latency bars, plugin breakdown, total latency, and PDC status.
/// </summary>
public partial class PluginLatencyView : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(PluginLatencyView),
            new PropertyMetadata(44100, OnSampleRateChanged));

    public static readonly DependencyProperty BufferSizeProperty =
        DependencyProperty.Register(nameof(BufferSize), typeof(int), typeof(PluginLatencyView),
            new PropertyMetadata(512, OnBufferSizeChanged));

    public static readonly DependencyProperty IsPdcEnabledProperty =
        DependencyProperty.Register(nameof(IsPdcEnabled), typeof(bool), typeof(PluginLatencyView),
            new PropertyMetadata(true, OnPdcStatusChanged));

    /// <summary>
    /// Gets or sets the current sample rate.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the current buffer size in samples.
    /// </summary>
    public int BufferSize
    {
        get => (int)GetValue(BufferSizeProperty);
        set => SetValue(BufferSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether PDC (Plugin Delay Compensation) is enabled.
    /// </summary>
    public bool IsPdcEnabled
    {
        get => (bool)GetValue(IsPdcEnabledProperty);
        set => SetValue(IsPdcEnabledProperty, value);
    }

    #endregion

    #region Private Fields

    private readonly ObservableCollection<LatencyEntry> _entries = new();
    private int _maxLatencySamples = 1;

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a refresh is requested.
    /// </summary>
    public event EventHandler? RefreshRequested;

    #endregion

    #region Constructor

    public PluginLatencyView()
    {
        InitializeComponent();
        LatencyItemsControl.ItemsSource = _entries;
        UpdateInfoDisplay();
    }

    #endregion

    #region Event Handlers

    private static void OnSampleRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PluginLatencyView view)
        {
            view.UpdateInfoDisplay();
            view.RecalculateMilliseconds();
        }
    }

    private static void OnBufferSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PluginLatencyView view)
        {
            view.UpdateInfoDisplay();
        }
    }

    private static void OnPdcStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PluginLatencyView view)
        {
            view.UpdatePdcStatus();
        }
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears all latency entries.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
        _maxLatencySamples = 1;
        UpdateTotalLatency();
    }

    /// <summary>
    /// Adds a track latency entry.
    /// </summary>
    /// <param name="trackName">The track name.</param>
    /// <param name="latencySamples">Total latency in samples for this track.</param>
    /// <param name="trackColor">The track color for visual identification.</param>
    public void AddTrackLatency(string trackName, int latencySamples, Color trackColor)
    {
        var entry = new LatencyEntry
        {
            Name = trackName,
            Samples = latencySamples,
            Milliseconds = SamplesToMs(latencySamples),
            Color = new SolidColorBrush(trackColor),
            BarColor = new SolidColorBrush(trackColor),
            IsPlugin = false,
            Indent = 0
        };

        _entries.Add(entry);
        UpdateMaxLatency(latencySamples);
        UpdateBarWidths();
        UpdateTotalLatency();
    }

    /// <summary>
    /// Adds a plugin latency entry under a track.
    /// </summary>
    /// <param name="pluginName">The plugin name.</param>
    /// <param name="latencySamples">The plugin's latency in samples.</param>
    public void AddPluginLatency(string pluginName, int latencySamples)
    {
        var entry = new LatencyEntry
        {
            Name = "  " + pluginName,
            Samples = latencySamples,
            Milliseconds = SamplesToMs(latencySamples),
            Color = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A)),
            BarColor = new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)),
            IsPlugin = true,
            Indent = 1
        };

        _entries.Add(entry);
        UpdateMaxLatency(latencySamples);
        UpdateBarWidths();
    }

    /// <summary>
    /// Sets the complete latency data for all tracks and plugins.
    /// </summary>
    /// <param name="trackLatencies">Dictionary of track names to (latency, color, plugin list).</param>
    public void SetLatencyData(Dictionary<string, (int latency, Color color, List<(string name, int latency)> plugins)> trackLatencies)
    {
        Clear();

        foreach (var (trackName, (latency, color, plugins)) in trackLatencies)
        {
            AddTrackLatency(trackName, latency, color);

            foreach (var (pluginName, pluginLatency) in plugins)
            {
                AddPluginLatency(pluginName, pluginLatency);
            }
        }

        UpdateBarWidths();
        UpdateTotalLatency();
    }

    #endregion

    #region Private Methods

    private void UpdateInfoDisplay()
    {
        SampleRateText.Text = $"{SampleRate} Hz";
        BufferSizeText.Text = $"{BufferSize} samples";
    }

    private void UpdatePdcStatus()
    {
        if (IsPdcEnabled)
        {
            PdcStatusIndicator.Fill = FindResource("SuccessBrush") as Brush ?? Brushes.Green;
            PdcStatusText.Text = "PDC Active";
        }
        else
        {
            PdcStatusIndicator.Fill = FindResource("WarningBrush") as Brush ?? Brushes.Orange;
            PdcStatusText.Text = "PDC Disabled";
        }
    }

    private void UpdateMaxLatency(int latencySamples)
    {
        if (latencySamples > _maxLatencySamples)
        {
            _maxLatencySamples = latencySamples;
        }
    }

    private void UpdateBarWidths()
    {
        const double maxBarWidth = 150; // Maximum bar width in pixels

        foreach (var entry in _entries)
        {
            double ratio = (double)entry.Samples / _maxLatencySamples;
            entry.BarWidth = Math.Max(2, ratio * maxBarWidth);
        }
    }

    private void UpdateTotalLatency()
    {
        int totalSamples = 0;

        foreach (var entry in _entries)
        {
            if (!entry.IsPlugin)
            {
                totalSamples = Math.Max(totalSamples, entry.Samples);
            }
        }

        double totalMs = SamplesToMs(totalSamples);
        TotalLatencyText.Text = $"{totalSamples} samples ({totalMs:F1} ms)";

        // Color code based on latency severity
        if (totalMs > 20)
        {
            TotalLatencyText.Foreground = FindResource("ErrorBrush") as Brush ?? Brushes.Red;
        }
        else if (totalMs > 10)
        {
            TotalLatencyText.Foreground = FindResource("WarningBrush") as Brush ?? Brushes.Orange;
        }
        else
        {
            TotalLatencyText.Foreground = FindResource("AccentBrush") as Brush ?? Brushes.Blue;
        }
    }

    private void RecalculateMilliseconds()
    {
        foreach (var entry in _entries)
        {
            entry.Milliseconds = SamplesToMs(entry.Samples);
        }
    }

    private double SamplesToMs(int samples)
    {
        if (SampleRate <= 0)
            return 0;
        return (double)samples / SampleRate * 1000.0;
    }

    #endregion
}
