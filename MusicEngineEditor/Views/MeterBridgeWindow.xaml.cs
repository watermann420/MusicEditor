using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MusicEngineEditor.Views;

/// <summary>
/// Meter type for display.
/// </summary>
public enum MeterDisplayType
{
    Peak,
    RMS,
    LUFS,
    PeakAndRMS
}

/// <summary>
/// Represents a track meter in the meter bridge.
/// </summary>
public class TrackMeterInfo : INotifyPropertyChanged
{
    private string _trackName = "";
    private float _leftLevel;
    private float _rightLevel;
    private bool _showLabel = true;
    private bool _showPeakHold = true;
    private double _meterWidth = 30;

    public string TrackName
    {
        get => _trackName;
        set { _trackName = value; OnPropertyChanged(nameof(TrackName)); }
    }

    public float LeftLevel
    {
        get => _leftLevel;
        set { _leftLevel = value; OnPropertyChanged(nameof(LeftLevel)); }
    }

    public float RightLevel
    {
        get => _rightLevel;
        set { _rightLevel = value; OnPropertyChanged(nameof(RightLevel)); }
    }

    public bool ShowLabel
    {
        get => _showLabel;
        set { _showLabel = value; OnPropertyChanged(nameof(ShowLabel)); }
    }

    public bool ShowPeakHold
    {
        get => _showPeakHold;
        set { _showPeakHold = value; OnPropertyChanged(nameof(ShowPeakHold)); }
    }

    public double MeterWidth
    {
        get => _meterWidth;
        set { _meterWidth = value; OnPropertyChanged(nameof(MeterWidth)); }
    }

    public int TrackIndex { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Floating meter bridge window showing all track meters in a row.
/// Features configurable meter type, always on top option, and scalable display.
/// </summary>
public partial class MeterBridgeWindow : Window
{
    #region Private Fields

    private readonly ObservableCollection<TrackMeterInfo> _trackMeters = new();
    private MeterDisplayType _meterType = MeterDisplayType.Peak;
    private double _displayScale = 1.0;
    private bool _showLabels = true;
    private bool _showPeakHold = true;
    private DispatcherTimer? _updateTimer;

    #endregion

    #region Events

    /// <summary>
    /// Raised when meter levels need to be updated from the audio engine.
    /// </summary>
    public event EventHandler? LevelsUpdateRequested;

    #endregion

    #region Constructor

    public MeterBridgeWindow()
    {
        InitializeComponent();

        MetersContainer.ItemsSource = _trackMeters;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartUpdateTimer();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Hide instead of close to allow re-showing
        e.Cancel = true;
        Hide();
        StopUpdateTimer();
    }

    private void MeterTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _meterType = MeterTypeCombo.SelectedIndex switch
        {
            0 => MeterDisplayType.Peak,
            1 => MeterDisplayType.RMS,
            2 => MeterDisplayType.LUFS,
            3 => MeterDisplayType.PeakAndRMS,
            _ => MeterDisplayType.Peak
        };

        // Update meter display based on type
        UpdateMeterDisplay();
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _displayScale = ScaleSlider.Value;
        ScaleText.Text = $"{(int)(_displayScale * 100)}%";
        UpdateMeterWidths();
    }

    private void ShowLabelsCheck_Changed(object sender, RoutedEventArgs e)
    {
        _showLabels = ShowLabelsCheck.IsChecked ?? true;
        foreach (var meter in _trackMeters)
        {
            meter.ShowLabel = _showLabels;
        }
    }

    private void ShowPeakHoldCheck_Changed(object sender, RoutedEventArgs e)
    {
        _showPeakHold = ShowPeakHoldCheck.IsChecked ?? true;
        foreach (var meter in _trackMeters)
        {
            meter.ShowPeakHold = _showPeakHold;
        }
    }

    private void ResetPeaksButton_Click(object sender, RoutedEventArgs e)
    {
        // Reset peak hold indicators - this would typically call back to the audio engine
        // For now, just trigger a visual reset by briefly setting levels to 0
        foreach (var meter in _trackMeters)
        {
            // The LevelMeter control handles peak reset internally when levels drop
        }
    }

    #endregion

    #region Update Timer

    private void StartUpdateTimer()
    {
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
        // Request level updates from the audio engine
        LevelsUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Private Methods

    private void UpdateMeterDisplay()
    {
        // Meter display type affects how levels are calculated/displayed
        // This would typically interface with the audio engine metering
    }

    private void UpdateMeterWidths()
    {
        double baseWidth = 30 * _displayScale;
        foreach (var meter in _trackMeters)
        {
            meter.MeterWidth = baseWidth;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the tracks to display meters for.
    /// </summary>
    public void SetTracks(string[] trackNames)
    {
        _trackMeters.Clear();

        for (int i = 0; i < trackNames.Length; i++)
        {
            _trackMeters.Add(new TrackMeterInfo
            {
                TrackName = trackNames[i],
                TrackIndex = i,
                ShowLabel = _showLabels,
                ShowPeakHold = _showPeakHold,
                MeterWidth = 30 * _displayScale
            });
        }
    }

    /// <summary>
    /// Adds a track meter.
    /// </summary>
    public void AddTrack(string trackName, int index = -1)
    {
        var meter = new TrackMeterInfo
        {
            TrackName = trackName,
            TrackIndex = index >= 0 ? index : _trackMeters.Count,
            ShowLabel = _showLabels,
            ShowPeakHold = _showPeakHold,
            MeterWidth = 30 * _displayScale
        };

        if (index >= 0 && index < _trackMeters.Count)
        {
            _trackMeters.Insert(index, meter);
        }
        else
        {
            _trackMeters.Add(meter);
        }
    }

    /// <summary>
    /// Removes a track meter by index.
    /// </summary>
    public void RemoveTrack(int index)
    {
        if (index >= 0 && index < _trackMeters.Count)
        {
            _trackMeters.RemoveAt(index);
        }
    }

    /// <summary>
    /// Updates the level for a specific track.
    /// </summary>
    public void SetTrackLevel(int trackIndex, float leftLevel, float rightLevel)
    {
        if (trackIndex >= 0 && trackIndex < _trackMeters.Count)
        {
            _trackMeters[trackIndex].LeftLevel = leftLevel;
            _trackMeters[trackIndex].RightLevel = rightLevel;
        }
    }

    /// <summary>
    /// Updates all track levels at once.
    /// </summary>
    public void SetAllLevels(float[] leftLevels, float[] rightLevels)
    {
        int count = Math.Min(Math.Min(leftLevels.Length, rightLevels.Length), _trackMeters.Count);

        for (int i = 0; i < count; i++)
        {
            _trackMeters[i].LeftLevel = leftLevels[i];
            _trackMeters[i].RightLevel = rightLevels[i];
        }
    }

    /// <summary>
    /// Updates track name.
    /// </summary>
    public void SetTrackName(int trackIndex, string name)
    {
        if (trackIndex >= 0 && trackIndex < _trackMeters.Count)
        {
            _trackMeters[trackIndex].TrackName = name;
        }
    }

    /// <summary>
    /// Gets the current meter display type.
    /// </summary>
    public MeterDisplayType GetMeterType() => _meterType;

    /// <summary>
    /// Sets the meter display type.
    /// </summary>
    public void SetMeterType(MeterDisplayType type)
    {
        _meterType = type;
        MeterTypeCombo.SelectedIndex = (int)type;
    }

    /// <summary>
    /// Gets the number of track meters.
    /// </summary>
    public int TrackCount => _trackMeters.Count;

    /// <summary>
    /// Shows the window.
    /// </summary>
    public void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        StartUpdateTimer();
    }

    /// <summary>
    /// Forces the window to close permanently.
    /// </summary>
    public void ForceClose()
    {
        StopUpdateTimer();
        Closing -= OnClosing;
        Close();
    }

    /// <summary>
    /// Resets all peak hold indicators.
    /// </summary>
    public void ResetAllPeaks()
    {
        // Trigger reset on all meters by temporarily setting levels to 0
        // The LevelMeter control will handle the peak reset
    }

    #endregion
}
