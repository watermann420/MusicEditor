// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Main analysis panel integrating tuner, chord, key, tempo, and loop detection tools.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Controls.Analysis;

/// <summary>
/// Main analysis panel providing integrated analysis tools including:
/// - Guitar Tuner
/// - Chord Detector
/// - Key Detector
/// - Tempo Detector
/// - Loop Finder
/// </summary>
public partial class AnalysisPanel : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty IsGloballyEnabledProperty =
        DependencyProperty.Register(nameof(IsGloballyEnabled), typeof(bool), typeof(AnalysisPanel),
            new PropertyMetadata(true, OnIsGloballyEnabledChanged));

    public static readonly DependencyProperty InputSourceProperty =
        DependencyProperty.Register(nameof(InputSource), typeof(AnalysisInputSource), typeof(AnalysisPanel),
            new PropertyMetadata(AnalysisInputSource.MasterOutput, OnInputSourceChanged));

    public static readonly DependencyProperty AnalysisQualityProperty =
        DependencyProperty.Register(nameof(AnalysisQuality), typeof(AnalysisQualityLevel), typeof(AnalysisPanel),
            new PropertyMetadata(AnalysisQualityLevel.Normal, OnAnalysisQualityChanged));

    /// <summary>
    /// Gets or sets whether analysis is globally enabled.
    /// </summary>
    public bool IsGloballyEnabled
    {
        get => (bool)GetValue(IsGloballyEnabledProperty);
        set => SetValue(IsGloballyEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the input source for analysis.
    /// </summary>
    public AnalysisInputSource InputSource
    {
        get => (AnalysisInputSource)GetValue(InputSourceProperty);
        set => SetValue(InputSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the analysis quality/resolution level.
    /// </summary>
    public AnalysisQualityLevel AnalysisQuality
    {
        get => (AnalysisQualityLevel)GetValue(AnalysisQualityProperty);
        set => SetValue(AnalysisQualityProperty, value);
    }

    #endregion

    #region Private Fields

    private readonly IntegratedAnalysisService _analysisService;
    private bool _isInitialized;

    // Tuner state
    private double _referencePitch = 440.0;

    // Chord history
    private readonly ObservableCollection<string> _chordHistory = new();
    private readonly ObservableCollection<string> _currentChordNotes = new();
    private const int MaxChordHistory = 20;

    // Key detection state
    private readonly ObservableCollection<string> _scaleNotes = new();
    private readonly List<Rectangle> _chromagramBars = new();

    // Loop detection state
    private readonly ObservableCollection<LoopPointViewModel> _detectedLoops = new();

    // Beat indicator state
#pragma warning disable CS0169
    private int _currentBeat;
#pragma warning restore CS0169
    private readonly Ellipse[] _beatIndicators = new Ellipse[4];

    // Colors
    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color SuccessColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color WarningColor = Color.FromRgb(0xFF, 0xB8, 0x00);
    private static readonly Color ErrorColor = Color.FromRgb(0xFF, 0x47, 0x57);
    private static readonly Color DimColor = Color.FromRgb(0x2A, 0x2A, 0x2A);

    #endregion

    #region Constructor

    public AnalysisPanel()
    {
        InitializeComponent();

        _analysisService = IntegratedAnalysisService.Instance;

        // Initialize collections
        ChordHistoryItemsControl.ItemsSource = _chordHistory;
        ChordNotesItemsControl.ItemsSource = _currentChordNotes;
        ScaleNotesItemsControl.ItemsSource = _scaleNotes;
        DetectedLoopsItemsControl.ItemsSource = _detectedLoops;

        // Store beat indicators for animation
        _beatIndicators[0] = BeatIndicator1;
        _beatIndicators[1] = BeatIndicator2;
        _beatIndicators[2] = BeatIndicator3;
        _beatIndicators[3] = BeatIndicator4;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        // Initialize chromagram visualization
        InitializeChromagram();

        // Subscribe to analysis service events
        SubscribeToAnalysisEvents();

        // Start analysis if globally enabled
        if (IsGloballyEnabled)
        {
            _analysisService.StartAnalysis();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;

        // Unsubscribe from events
        UnsubscribeFromAnalysisEvents();
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToAnalysisEvents()
    {
        _analysisService.TunerUpdated += OnTunerUpdated;
        _analysisService.ChordDetected += OnChordDetected;
        _analysisService.KeyDetected += OnKeyDetected;
        _analysisService.TempoDetected += OnTempoDetected;
        _analysisService.BeatDetected += OnBeatDetected;
        _analysisService.LoopsDetected += OnLoopsDetected;
        _analysisService.ChromagramUpdated += OnChromagramUpdated;
    }

    private void UnsubscribeFromAnalysisEvents()
    {
        _analysisService.TunerUpdated -= OnTunerUpdated;
        _analysisService.ChordDetected -= OnChordDetected;
        _analysisService.KeyDetected -= OnKeyDetected;
        _analysisService.TempoDetected -= OnTempoDetected;
        _analysisService.BeatDetected -= OnBeatDetected;
        _analysisService.LoopsDetected -= OnLoopsDetected;
        _analysisService.ChromagramUpdated -= OnChromagramUpdated;
    }

    #endregion

    #region Property Changed Handlers

    private static void OnIsGloballyEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnalysisPanel panel && panel._isInitialized)
        {
            var enabled = (bool)e.NewValue;
            if (enabled)
            {
                panel._analysisService.StartAnalysis();
            }
            else
            {
                panel._analysisService.StopAnalysis();
            }
        }
    }

    private static void OnInputSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnalysisPanel panel && panel._isInitialized)
        {
            panel._analysisService.InputSource = (AnalysisInputSource)e.NewValue;
        }
    }

    private static void OnAnalysisQualityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnalysisPanel panel && panel._isInitialized)
        {
            panel._analysisService.Quality = (AnalysisQualityLevel)e.NewValue;
        }
    }

    #endregion

    #region UI Event Handlers

    private void GlobalEnableToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (GlobalEnableToggle == null) return; // Guard during initialization
        IsGloballyEnabled = GlobalEnableToggle.IsChecked == true;
    }

    private void InputSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;

        InputSource = InputSourceComboBox.SelectedIndex switch
        {
            0 => AnalysisInputSource.MasterOutput,
            1 => AnalysisInputSource.SelectedTrack,
            2 => AnalysisInputSource.ExternalInput,
            _ => AnalysisInputSource.MasterOutput
        };
    }

    private void Quality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;

        AnalysisQuality = QualityComboBox.SelectedIndex switch
        {
            0 => AnalysisQualityLevel.Low,
            1 => AnalysisQualityLevel.Normal,
            2 => AnalysisQualityLevel.High,
            _ => AnalysisQualityLevel.Normal
        };
    }

    private void AnalysisTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;

        // Notify service which analysis type is active for optimization
        var activeTab = (AnalysisTabControl.SelectedIndex) switch
        {
            0 => AnalysisType.Tuner,
            1 => AnalysisType.Chord,
            2 => AnalysisType.Key,
            3 => AnalysisType.Tempo,
            4 => AnalysisType.Loop,
            _ => AnalysisType.Tuner
        };

        _analysisService.SetActiveAnalysisType(activeTab);
    }

    private void ReferencePitch_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        _referencePitch = ReferencePitchSlider.Value;
        ReferencePitchText.Text = $"{_referencePitch:F0} Hz";
        _analysisService.ReferencePitch = _referencePitch;
    }

    private void ClearChordHistory_Click(object sender, RoutedEventArgs e)
    {
        _chordHistory.Clear();
    }

    private void TapTempo_Click(object sender, RoutedEventArgs e)
    {
        var bpm = _analysisService.Tap();
        UpdateTapTempoDisplay(bpm, _analysisService.TapCount);
    }

    private void ResetTapTempo_Click(object sender, RoutedEventArgs e)
    {
        _analysisService.ResetTapTempo();
        TapTempoBpmText.Text = "-- BPM";
        TapCountText.Text = "0 taps";
    }

    private void AnalyzeLoops_Click(object sender, RoutedEventArgs e)
    {
        LoopFinderStatusText.Text = "Analyzing...";
        _analysisService.FindLoops();
    }

    private void UseLoop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is LoopPointViewModel loop)
        {
            _analysisService.ApplyLoopPoint(loop.StartTime, loop.EndTime);
        }
    }

    private void MinLoopLength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        var value = (int)MinLoopLengthSlider.Value;
        MinLoopLengthText.Text = value.ToString();
        _analysisService.MinLoopBars = value;
    }

    private void MaxLoopLength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        var value = (int)MaxLoopLengthSlider.Value;
        MaxLoopLengthText.Text = value.ToString();
        _analysisService.MaxLoopBars = value;
    }

    #endregion

    #region Analysis Event Handlers

    private void OnTunerUpdated(object? sender, TunerEventArgs e)
    {
        if (!_isInitialized) return;

        Dispatcher.BeginInvoke(() =>
        {
            UpdateTunerDisplay(e);
        }, DispatcherPriority.Render);
    }

    private void OnChordDetected(object? sender, ChordEventArgs e)
    {
        if (!_isInitialized) return;

        Dispatcher.BeginInvoke(() =>
        {
            UpdateChordDisplay(e);
        }, DispatcherPriority.Render);
    }

    private void OnKeyDetected(object? sender, MusicalKeyEventArgs e)
    {
        if (!_isInitialized) return;

        Dispatcher.BeginInvoke(() =>
        {
            UpdateKeyDisplay(e);
        }, DispatcherPriority.Render);
    }

    private void OnTempoDetected(object? sender, TempoEventArgs e)
    {
        if (!_isInitialized) return;

        Dispatcher.BeginInvoke(() =>
        {
            UpdateTempoDisplay(e);
        }, DispatcherPriority.Render);
    }

    private void OnBeatDetected(object? sender, BeatEventArgs e)
    {
        if (!_isInitialized) return;

        Dispatcher.BeginInvoke(() =>
        {
            UpdateBeatIndicator(e.BeatNumber);
        }, DispatcherPriority.Render);
    }

    private void OnLoopsDetected(object? sender, LoopDetectionEventArgs e)
    {
        if (!_isInitialized) return;

        Dispatcher.BeginInvoke(() =>
        {
            UpdateLoopDisplay(e);
        }, DispatcherPriority.Render);
    }

    private void OnChromagramUpdated(object? sender, ChromagramEventArgs e)
    {
        if (!_isInitialized) return;

        Dispatcher.BeginInvoke(() =>
        {
            UpdateChromagram(e.Values);
        }, DispatcherPriority.Render);
    }

    #endregion

    #region Display Update Methods

    private void UpdateTunerDisplay(TunerEventArgs e)
    {
        // Note name
        TunerNoteText.Text = e.NoteName ?? "--";
        TunerOctaveText.Text = e.Octave >= 0 ? $"Octave {e.Octave}" : "";

        // Frequency
        TunerFrequencyText.Text = e.Frequency > 0 ? $"{e.Frequency:F1} Hz" : "-- Hz";

        // Cents deviation
        TunerCentsText.Text = e.CentsDeviation >= 0 ? $"+{e.CentsDeviation:F0}" : $"{e.CentsDeviation:F0}";

        // Needle position (map cents to pixels, -50 to +50 cents = full range)
        double maxOffset = 100; // pixels
        double offset = Math.Clamp(e.CentsDeviation / 50.0 * maxOffset, -maxOffset, maxOffset);
        TunerNeedleTransform.X = offset;

        // Needle and cents color based on accuracy
        Color color;
        if (Math.Abs(e.CentsDeviation) <= 3)
        {
            color = SuccessColor;
        }
        else if (Math.Abs(e.CentsDeviation) <= 10)
        {
            color = WarningColor;
        }
        else
        {
            color = ErrorColor;
        }

        TunerNeedle.Background = new SolidColorBrush(color);
        TunerCentsText.Foreground = new SolidColorBrush(color);
    }

    private void UpdateChordDisplay(ChordEventArgs e)
    {
        // Chord name
        ChordNameText.Text = e.RootNote ?? "--";
        ChordTypeText.Text = e.ChordType ?? "";

        // Confidence
        ChordConfidenceText.Text = $"{e.Confidence * 100:F0}%";
        var parentWidth = ((Border)ChordConfidenceFill.Parent).ActualWidth;
        ChordConfidenceFill.Width = parentWidth > 0 ? parentWidth * e.Confidence : 0;

        // Notes in chord
        _currentChordNotes.Clear();
        if (e.Notes != null && e.Notes.Length > 0)
        {
            foreach (var note in e.Notes)
            {
                _currentChordNotes.Add(note);
            }
            NoChordNotesText.Visibility = Visibility.Collapsed;
        }
        else
        {
            NoChordNotesText.Visibility = Visibility.Visible;
        }

        // Add to history if valid chord
        if (!string.IsNullOrEmpty(e.RootNote) && e.Confidence > 0.5)
        {
            var chordName = $"{e.RootNote}{e.ChordType}";
            if (_chordHistory.Count == 0 || _chordHistory[0] != chordName)
            {
                _chordHistory.Insert(0, chordName);
                while (_chordHistory.Count > MaxChordHistory)
                {
                    _chordHistory.RemoveAt(_chordHistory.Count - 1);
                }
            }
        }
    }

    private void UpdateKeyDisplay(MusicalKeyEventArgs e)
    {
        // Key name
        KeyRootText.Text = e.RootNote ?? "--";
        KeyModeText.Text = e.Mode ?? "";

        // Relative key
        if (!string.IsNullOrEmpty(e.RelativeKey))
        {
            RelativeKeyText.Text = $"Relative: {e.RelativeKey}";
        }
        else
        {
            RelativeKeyText.Text = "";
        }

        // Confidence
        KeyConfidenceText.Text = $"{e.Confidence * 100:F0}%";
        var parentWidth = ((Border)KeyConfidenceFill.Parent).ActualWidth;
        KeyConfidenceFill.Width = parentWidth > 0 ? parentWidth * e.Confidence : 0;

        // Scale notes
        _scaleNotes.Clear();
        if (e.ScaleNotes != null)
        {
            foreach (var note in e.ScaleNotes)
            {
                _scaleNotes.Add(note);
            }
        }
    }

    private void UpdateTempoDisplay(TempoEventArgs e)
    {
        // BPM
        TempoBpmText.Text = e.Bpm > 0 ? $"{e.Bpm:F1}" : "--";

        // Confidence
        TempoConfidenceText.Text = $"{e.Confidence * 100:F0}%";
        var parentWidth = ((Border)TempoConfidenceFill.Parent).ActualWidth;
        TempoConfidenceFill.Width = parentWidth > 0 ? parentWidth * e.Confidence : 0;

        // Time signature
        TimeSignatureText.Text = e.TimeSignature ?? "4/4";
    }

    private void UpdateBeatIndicator(int beatNumber)
    {
        // Reset all indicators
        for (int i = 0; i < 4; i++)
        {
            _beatIndicators[i].Fill = new SolidColorBrush(DimColor);
        }

        // Highlight current beat
        int index = (beatNumber - 1) % 4;
        if (index >= 0 && index < 4)
        {
            _beatIndicators[index].Fill = new SolidColorBrush(index == 0 ? AccentColor : SuccessColor);
        }
    }

    private void UpdateTapTempoDisplay(double bpm, int tapCount)
    {
        TapTempoBpmText.Text = bpm > 0 ? $"{bpm:F1} BPM" : "-- BPM";
        TapCountText.Text = $"{tapCount} tap{(tapCount != 1 ? "s" : "")}";
    }

    private void UpdateLoopDisplay(LoopDetectionEventArgs e)
    {
        LoopFinderStatusText.Text = e.IsComplete ? "Analysis complete" : "Analyzing...";

        _detectedLoops.Clear();
        if (e.Loops != null && e.Loops.Length > 0)
        {
            foreach (var loop in e.Loops)
            {
                _detectedLoops.Add(new LoopPointViewModel
                {
                    Name = loop.Name,
                    StartTime = loop.StartTime,
                    EndTime = loop.EndTime,
                    Bars = loop.Bars,
                    Score = loop.Score
                });
            }
            NoLoopsText.Visibility = Visibility.Collapsed;
        }
        else
        {
            NoLoopsText.Visibility = Visibility.Visible;
        }
    }

    #endregion

    #region Chromagram Visualization

    private void InitializeChromagram()
    {
        ChromagramCanvas.Children.Clear();
        _chromagramBars.Clear();

        // Create 12 bars for each pitch class
        for (int i = 0; i < 12; i++)
        {
            var bar = new Rectangle
            {
                Fill = new SolidColorBrush(AccentColor),
                Opacity = 0.3,
                RadiusX = 2,
                RadiusY = 2
            };

            _chromagramBars.Add(bar);
            ChromagramCanvas.Children.Add(bar);
        }

        // Update sizes on canvas size change
        ChromagramCanvas.SizeChanged += (s, e) => UpdateChromagramLayout();
        UpdateChromagramLayout();
    }

    private void UpdateChromagramLayout()
    {
        double width = ChromagramCanvas.ActualWidth;
        double height = ChromagramCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double barWidth = (width - 22) / 12; // 2px gap between bars
        double gap = 2;

        for (int i = 0; i < _chromagramBars.Count; i++)
        {
            var bar = _chromagramBars[i];
            bar.Width = barWidth - gap;
            Canvas.SetLeft(bar, i * barWidth + gap / 2);
            Canvas.SetBottom(bar, 0);
        }
    }

    private void UpdateChromagram(float[]? values)
    {
        if (values == null || values.Length != 12) return;

        double height = ChromagramCanvas.ActualHeight;
        if (height <= 0) return;

        for (int i = 0; i < 12 && i < _chromagramBars.Count; i++)
        {
            var value = Math.Clamp(values[i], 0, 1);
            _chromagramBars[i].Height = Math.Max(2, value * height);
            _chromagramBars[i].Opacity = 0.3 + (value * 0.7);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets all analysis state and clears history.
    /// </summary>
    public void Reset()
    {
        _analysisService.Reset();
        _chordHistory.Clear();
        _detectedLoops.Clear();
        _currentChordNotes.Clear();
        _scaleNotes.Clear();

        // Reset displays
        TunerNoteText.Text = "--";
        TunerOctaveText.Text = "";
        TunerFrequencyText.Text = "-- Hz";
        TunerCentsText.Text = "0";
        TunerNeedleTransform.X = 0;

        ChordNameText.Text = "--";
        ChordTypeText.Text = "";
        ChordConfidenceFill.Width = 0;

        KeyRootText.Text = "--";
        KeyModeText.Text = "";
        RelativeKeyText.Text = "";
        KeyConfidenceFill.Width = 0;

        TempoBpmText.Text = "--";
        TempoConfidenceFill.Width = 0;
        TimeSignatureText.Text = "4/4";

        TapTempoBpmText.Text = "-- BPM";
        TapCountText.Text = "0 taps";

        LoopFinderStatusText.Text = "Select audio to analyze";
        NoLoopsText.Visibility = Visibility.Visible;

        // Reset beat indicators
        for (int i = 0; i < 4; i++)
        {
            _beatIndicators[i].Fill = new SolidColorBrush(DimColor);
        }
    }

    /// <summary>
    /// Starts all analysis.
    /// </summary>
    public void StartAnalysis()
    {
        IsGloballyEnabled = true;
    }

    /// <summary>
    /// Stops all analysis.
    /// </summary>
    public void StopAnalysis()
    {
        IsGloballyEnabled = false;
    }

    #endregion
}

#region Enums

/// <summary>
/// Input source for analysis.
/// </summary>
public enum AnalysisInputSource
{
    MasterOutput,
    SelectedTrack,
    ExternalInput
}

/// <summary>
/// Analysis quality/resolution level.
/// </summary>
public enum AnalysisQualityLevel
{
    Low,
    Normal,
    High
}

/// <summary>
/// Type of analysis being performed.
/// </summary>
public enum AnalysisType
{
    Tuner,
    Chord,
    Key,
    Tempo,
    Loop
}

#endregion

#region Event Args

/// <summary>
/// Event arguments for tuner updates.
/// </summary>
public class TunerEventArgs : EventArgs
{
    public string? NoteName { get; set; }
    public int Octave { get; set; }
    public double Frequency { get; set; }
    public double CentsDeviation { get; set; }
}

/// <summary>
/// Event arguments for chord detection.
/// </summary>
public class ChordEventArgs : EventArgs
{
    public string? RootNote { get; set; }
    public string? ChordType { get; set; }
    public string[]? Notes { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// Event arguments for musical key detection.
/// </summary>
public class MusicalKeyEventArgs : EventArgs
{
    public string? RootNote { get; set; }
    public string? Mode { get; set; }
    public string? RelativeKey { get; set; }
    public string[]? ScaleNotes { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// Event arguments for tempo detection.
/// </summary>
public class TempoEventArgs : EventArgs
{
    public double Bpm { get; set; }
    public double Confidence { get; set; }
    public string? TimeSignature { get; set; }
}

/// <summary>
/// Event arguments for beat detection.
/// </summary>
public class BeatEventArgs : EventArgs
{
    public int BeatNumber { get; set; }
    public double Timestamp { get; set; }
}

/// <summary>
/// Event arguments for loop detection.
/// </summary>
public class LoopDetectionEventArgs : EventArgs
{
    public bool IsComplete { get; set; }
    public LoopPoint[]? Loops { get; set; }
}

/// <summary>
/// Event arguments for chromagram updates.
/// </summary>
public class ChromagramEventArgs : EventArgs
{
    public float[]? Values { get; set; }
}

/// <summary>
/// Represents a detected loop point.
/// </summary>
public class LoopPoint
{
    public string Name { get; set; } = "";
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public int Bars { get; set; }
    public double Score { get; set; }
}

#endregion

#region View Models

/// <summary>
/// View model for loop point display.
/// </summary>
public class LoopPointViewModel : INotifyPropertyChanged
{
    private string _name = "";
    private double _startTime;
    private double _endTime;
    private int _bars;
    private double _score;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    public double StartTime
    {
        get => _startTime;
        set { _startTime = value; OnPropertyChanged(nameof(StartTime)); }
    }

    public double EndTime
    {
        get => _endTime;
        set { _endTime = value; OnPropertyChanged(nameof(EndTime)); }
    }

    public int Bars
    {
        get => _bars;
        set { _bars = value; OnPropertyChanged(nameof(Bars)); }
    }

    public double Score
    {
        get => _score;
        set { _score = value; OnPropertyChanged(nameof(Score)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

#endregion
