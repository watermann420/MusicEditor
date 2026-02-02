// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Tempo Detector Panel for comprehensive tempo analysis and detection.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngineEditor.ViewModels.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Analysis;

/// <summary>
/// Tempo Detector Panel providing BPM detection, tap tempo, beat grid visualization,
/// time signature detection, tempo variation analysis, and project tempo synchronization.
/// </summary>
public partial class TempoDetectorPanel : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(TempoDetectorPanelViewModel), typeof(TempoDetectorPanel),
            new PropertyMetadata(null, OnViewModelChanged));

    public static readonly DependencyProperty AudioSamplesProperty =
        DependencyProperty.Register(nameof(AudioSamples), typeof(float[]), typeof(TempoDetectorPanel),
            new PropertyMetadata(null, OnAudioSamplesChanged));

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(TempoDetectorPanel),
            new PropertyMetadata(44100));

    public static readonly DependencyProperty WaveformDataProperty =
        DependencyProperty.Register(nameof(WaveformData), typeof(float[]), typeof(TempoDetectorPanel),
            new PropertyMetadata(null, OnWaveformDataChanged));

    public static readonly DependencyProperty AudioDurationProperty =
        DependencyProperty.Register(nameof(AudioDuration), typeof(double), typeof(TempoDetectorPanel),
            new PropertyMetadata(0.0));

    /// <summary>Gets or sets the ViewModel.</summary>
    public TempoDetectorPanelViewModel? ViewModel
    {
        get => (TempoDetectorPanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Gets or sets the audio samples for analysis.</summary>
    public float[]? AudioSamples
    {
        get => (float[]?)GetValue(AudioSamplesProperty);
        set => SetValue(AudioSamplesProperty, value);
    }

    /// <summary>Gets or sets the sample rate.</summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>Gets or sets the waveform data for visualization.</summary>
    public float[]? WaveformData
    {
        get => (float[]?)GetValue(WaveformDataProperty);
        set => SetValue(WaveformDataProperty, value);
    }

    /// <summary>Gets or sets the audio duration in seconds.</summary>
    public double AudioDuration
    {
        get => (double)GetValue(AudioDurationProperty);
        set => SetValue(AudioDurationProperty, value);
    }

    #endregion

    #region Events

    /// <summary>Event raised when tempo should be applied to the project.</summary>
    public event EventHandler<TempoApplyEventArgs>? ApplyTempoRequested;

    /// <summary>Event raised when analysis completes.</summary>
    public event EventHandler<TempoAnalysisCompletedEventArgs>? AnalysisCompleted;

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private TempoDetectorPanelViewModel _internalViewModel;
    private DispatcherTimer? _animationTimer;
    private int _animationFrame;

    // Graph elements
    private Shapes.Polyline? _waveformLine;
    private Shapes.Polyline? _variationLine;
    private readonly List<Shapes.Line> _beatLines = new();

    // Theme colors
    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color SuccessColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color WarningColor = Color.FromRgb(0xFF, 0xB8, 0x00);
    private static readonly Color ErrorColor = Color.FromRgb(0xFF, 0x47, 0x57);
    private static readonly Color WaveformColor = Color.FromRgb(0x3A, 0x3A, 0x3A);
    private static readonly Color BeatColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color DownbeatColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color GridColor = Color.FromRgb(0x2A, 0x2A, 0x2A);

    #endregion

    #region Constructor

    public TempoDetectorPanel()
    {
        InitializeComponent();

        _internalViewModel = new TempoDetectorPanelViewModel();
        SubscribeToViewModel(_internalViewModel);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        KeyDown += OnKeyDown;

        // Set focus to receive keyboard input
        Focusable = true;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        InitializeGraphElements();
        UpdateDisplay();

        // Start animation timer for analyzing indicator
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _animationTimer?.Stop();

        if (ViewModel != null)
        {
            UnsubscribeFromViewModel(ViewModel);
        }
        UnsubscribeFromViewModel(_internalViewModel);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            CurrentViewModel.TapCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TempoDetectorPanel panel)
        {
            if (e.OldValue is TempoDetectorPanelViewModel oldVm)
            {
                panel.UnsubscribeFromViewModel(oldVm);
            }

            if (e.NewValue is TempoDetectorPanelViewModel newVm)
            {
                panel.SubscribeToViewModel(newVm);
            }

            if (panel._isInitialized)
            {
                panel.UpdateDisplay();
            }
        }
    }

    private static void OnAudioSamplesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TempoDetectorPanel panel && e.NewValue is float[] samples && samples.Length > 0)
        {
            panel.CurrentViewModel.Analyze(samples, panel.SampleRate);
        }
    }

    private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TempoDetectorPanel panel && panel._isInitialized)
        {
            panel.DrawWaveform();
        }
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        if (CurrentViewModel.IsAnalyzing)
        {
            _animationFrame = (_animationFrame + 1) % 3;
            AnalyzingIndicator.Opacity = 0.5 + (_animationFrame * 0.25);
        }
    }

    #endregion

    #region ViewModel Subscription

    private TempoDetectorPanelViewModel CurrentViewModel => ViewModel ?? _internalViewModel;

    private void SubscribeToViewModel(TempoDetectorPanelViewModel vm)
    {
        vm.PropertyChanged += ViewModel_PropertyChanged;
        vm.ApplyTempoRequested += ViewModel_ApplyTempoRequested;
        vm.AnalysisCompleted += ViewModel_AnalysisCompleted;
    }

    private void UnsubscribeFromViewModel(TempoDetectorPanelViewModel vm)
    {
        vm.PropertyChanged -= ViewModel_PropertyChanged;
        vm.ApplyTempoRequested -= ViewModel_ApplyTempoRequested;
        vm.AnalysisCompleted -= ViewModel_AnalysisCompleted;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_isInitialized) return;

        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(TempoDetectorPanelViewModel.DetectedBpm):
                    UpdateBpmDisplay();
                    UpdateAlternativeTempos();
                    break;
                case nameof(TempoDetectorPanelViewModel.TapTempoBpm):
                    UpdateTapTempoDisplay();
                    break;
                case nameof(TempoDetectorPanelViewModel.Confidence):
                case nameof(TempoDetectorPanelViewModel.ConfidenceLevel):
                    UpdateConfidenceDisplay();
                    break;
                case nameof(TempoDetectorPanelViewModel.TapCount):
                    UpdateTapCountDisplay();
                    break;
                case nameof(TempoDetectorPanelViewModel.IsAnalyzing):
                    UpdateAnalyzingState();
                    break;
                case nameof(TempoDetectorPanelViewModel.CanApplyTempo):
                    ApplyButton.IsEnabled = CurrentViewModel.CanApplyTempo;
                    break;
                case nameof(TempoDetectorPanelViewModel.DetectedTimeSignature):
                    UpdateTimeSignatureDisplay();
                    break;
                case nameof(TempoDetectorPanelViewModel.BeatPositions):
                    DrawBeatGrid();
                    break;
                case nameof(TempoDetectorPanelViewModel.TempoVariations):
                    DrawVariationGraph();
                    break;
                case nameof(TempoDetectorPanelViewModel.IsDownbeatDetected):
                    UpdateDownbeatIndicator();
                    break;
                case nameof(TempoDetectorPanelViewModel.HasStableTempo):
                case nameof(TempoDetectorPanelViewModel.AverageTempoVariation):
                    UpdateTempoStabilityDisplay();
                    break;
            }
        });
    }

    private void ViewModel_ApplyTempoRequested(object? sender, TempoApplyEventArgs e)
    {
        ApplyTempoRequested?.Invoke(this, e);
    }

    private void ViewModel_AnalysisCompleted(object? sender, TempoAnalysisCompletedEventArgs e)
    {
        AnalysisCompleted?.Invoke(this, e);
    }

    #endregion

    #region Button Click Handlers

    private void TapButton_Click(object sender, RoutedEventArgs e)
    {
        CurrentViewModel.TapCommand.Execute(null);
        Focus(); // Keep focus for keyboard input
    }

    private void UseTapButton_Click(object sender, RoutedEventArgs e)
    {
        CurrentViewModel.UseTapTempoCommand.Execute(null);
    }

    private void ResetTapButton_Click(object sender, RoutedEventArgs e)
    {
        CurrentViewModel.ResetTapTempoCommand.Execute(null);
    }

    private void HalfTimeButton_Click(object sender, RoutedEventArgs e)
    {
        CurrentViewModel.UseHalfTimeCommand.Execute(null);
    }

    private void DoubleTimeButton_Click(object sender, RoutedEventArgs e)
    {
        CurrentViewModel.UseDoubleTimeCommand.Execute(null);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        CurrentViewModel.ApplyTempoCommand.Execute(null);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        CurrentViewModel.ResetCommand.Execute(null);
        ClearGraphs();
    }

    private void TimeSignatureButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button && button.IsChecked == true)
        {
            // Uncheck other buttons
            if (button != Sig44Button) Sig44Button.IsChecked = false;
            if (button != Sig34Button) Sig34Button.IsChecked = false;
            if (button != Sig68Button) Sig68Button.IsChecked = false;

            string sig = button.Content.ToString() ?? "4/4";
            CurrentViewModel.SetTimeSignatureCommand.Execute(sig);
        }
    }

    private void MetronomeSyncToggle_Changed(object sender, RoutedEventArgs e)
    {
        bool isChecked = MetronomeSyncToggle.IsChecked ?? false;
        CurrentViewModel.IsMetronomeSyncEnabled = isChecked;
        MetronomeSyncToggle.Content = isChecked ? "On" : "Off";
    }

    private void MinBpmTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (double.TryParse(MinBpmTextBox.Text, out double minBpm) && minBpm > 0)
        {
            CurrentViewModel.MinBpm = Math.Clamp(minBpm, 20, 300);
        }
    }

    private void MaxBpmTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (double.TryParse(MaxBpmTextBox.Text, out double maxBpm) && maxBpm > 0)
        {
            CurrentViewModel.MaxBpm = Math.Clamp(maxBpm, 40, 400);
        }
    }

    private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitialized)
        {
            CurrentViewModel.OnsetSensitivity = SensitivitySlider.Value;
            SensitivityValueText.Text = $"{SensitivitySlider.Value * 100:F0}%";
        }
    }

    private void BeatGridCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawWaveform();
            DrawBeatGrid();
        }
    }

    private void VariationGraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawVariationGraph();
        }
    }

    #endregion

    #region Initialization

    private void InitializeGraphElements()
    {
        // Initialize waveform line
        _waveformLine = new Shapes.Polyline
        {
            Stroke = new SolidColorBrush(WaveformColor),
            StrokeThickness = 1,
            StrokeLineJoin = PenLineJoin.Round
        };
        BeatGridCanvas.Children.Add(_waveformLine);

        // Initialize variation line
        _variationLine = new Shapes.Polyline
        {
            Stroke = new SolidColorBrush(AccentColor),
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        VariationGraphCanvas.Children.Add(_variationLine);

        // Draw initial grid
        DrawVariationGridLines();
    }

    #endregion

    #region Display Update Methods

    private void UpdateDisplay()
    {
        UpdateBpmDisplay();
        UpdateTapTempoDisplay();
        UpdateConfidenceDisplay();
        UpdateTapCountDisplay();
        UpdateAlternativeTempos();
        UpdateTimeSignatureDisplay();
        UpdateDownbeatIndicator();
        UpdateTempoStabilityDisplay();
        UpdateAnalyzingState();
        ApplyButton.IsEnabled = CurrentViewModel.CanApplyTempo;
    }

    private void UpdateBpmDisplay()
    {
        double bpm = CurrentViewModel.DetectedBpm;
        BpmDisplayText.Text = bpm > 0 ? bpm.ToString("F1") : "---";
    }

    private void UpdateTapTempoDisplay()
    {
        double bpm = CurrentViewModel.TapTempoBpm;
        TapBpmText.Text = bpm > 0 ? bpm.ToString("F1") : "---";
    }

    private void UpdateConfidenceDisplay()
    {
        double confidence = CurrentViewModel.Confidence;
        double percent = confidence * 100;

        ConfidenceText.Text = $"{percent:F0}%";
        ConfidenceLevelText.Text = CurrentViewModel.ConfidenceLevel;

        // Update fill height (max 80px)
        ConfidenceFill.Height = confidence * 80;

        // Update color based on confidence level
        Color fillColor = confidence switch
        {
            >= 0.8 => SuccessColor,
            >= 0.5 => WarningColor,
            _ => ErrorColor
        };
        ConfidenceFill.Background = new SolidColorBrush(fillColor);
    }

    private void UpdateTapCountDisplay()
    {
        TapCountText.Text = $"{CurrentViewModel.TapCount} taps";
    }

    private void UpdateAlternativeTempos()
    {
        double halfTime = CurrentViewModel.HalfTimeBpm;
        double doubleTime = CurrentViewModel.DoubleTimeBpm;

        HalfTimeBpmText.Text = halfTime > 0 ? halfTime.ToString("F1") : "---";
        DoubleTimeBpmText.Text = doubleTime > 0 ? doubleTime.ToString("F1") : "---";

        HalfTimeButton.IsEnabled = halfTime >= CurrentViewModel.MinBpm;
        DoubleTimeButton.IsEnabled = doubleTime <= CurrentViewModel.MaxBpm;
    }

    private void UpdateTimeSignatureDisplay()
    {
        TimeSignatureText.Text = CurrentViewModel.DetectedTimeSignature;

        // Update toggle buttons
        string sig = CurrentViewModel.DetectedTimeSignature;
        Sig44Button.IsChecked = sig == "4/4";
        Sig34Button.IsChecked = sig == "3/4";
        Sig68Button.IsChecked = sig == "6/8";
    }

    private void UpdateDownbeatIndicator()
    {
        DownbeatIndicator.Visibility = CurrentViewModel.IsDownbeatDetected
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateTempoStabilityDisplay()
    {
        bool isStable = CurrentViewModel.HasStableTempo;
        double variation = CurrentViewModel.AverageTempoVariation;

        TempoStabilityText.Text = isStable ? "Stable" : "Variable";
        TempoStabilityText.Foreground = new SolidColorBrush(isStable ? SuccessColor : WarningColor);
        VariationPercentText.Text = $"{variation:F1}%";
    }

    private void UpdateAnalyzingState()
    {
        bool isAnalyzing = CurrentViewModel.IsAnalyzing;
        AnalyzingIndicator.Visibility = isAnalyzing ? Visibility.Visible : Visibility.Collapsed;

        if (isAnalyzing)
        {
            _animationTimer?.Start();
        }
        else
        {
            _animationTimer?.Stop();
        }
    }

    #endregion

    #region Drawing Methods

    private void DrawWaveform()
    {
        if (_waveformLine == null) return;

        _waveformLine.Points.Clear();

        float[]? waveform = WaveformData ?? CurrentViewModel.WaveformData;
        if (waveform == null || waveform.Length == 0) return;

        double width = BeatGridCanvas.ActualWidth;
        double height = BeatGridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double centerY = height / 2;
        double amplitude = height / 2 - 5;

        var points = new PointCollection();
        int step = Math.Max(1, waveform.Length / (int)width);

        for (int i = 0; i < waveform.Length; i += step)
        {
            double x = (double)i / waveform.Length * width;
            double y = centerY - waveform[i] * amplitude;
            points.Add(new Point(x, Math.Clamp(y, 0, height)));
        }

        _waveformLine.Points = points;
    }

    private void DrawBeatGrid()
    {
        // Clear existing beat lines
        foreach (var line in _beatLines)
        {
            BeatGridCanvas.Children.Remove(line);
        }
        _beatLines.Clear();

        var beatPositions = CurrentViewModel.BeatPositions;
        double duration = CurrentViewModel.AudioDuration;

        if (beatPositions == null || beatPositions.Count == 0 || duration <= 0) return;

        double width = BeatGridCanvas.ActualWidth;
        double height = BeatGridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        int beatsPerMeasure = CurrentViewModel.BeatsPerMeasure;
        int beatIndex = 0;

        foreach (double beatTime in beatPositions)
        {
            double x = (beatTime / duration) * width;
            bool isDownbeat = beatIndex % beatsPerMeasure == 0;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = new SolidColorBrush(isDownbeat ? DownbeatColor : BeatColor),
                StrokeThickness = isDownbeat ? 2 : 1,
                Opacity = isDownbeat ? 0.8 : 0.5
            };

            BeatGridCanvas.Children.Add(line);
            _beatLines.Add(line);

            beatIndex++;
        }
    }

    private void DrawVariationGridLines()
    {
        double width = VariationGraphCanvas.ActualWidth;
        double height = VariationGraphCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var gridBrush = new SolidColorBrush(GridColor);

        // Horizontal center line (average BPM)
        var centerLine = new Shapes.Line
        {
            X1 = 0,
            Y1 = height / 2,
            X2 = width,
            Y2 = height / 2,
            Stroke = gridBrush,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        VariationGraphCanvas.Children.Insert(0, centerLine);
    }

    private void DrawVariationGraph()
    {
        if (_variationLine == null) return;

        _variationLine.Points.Clear();

        var variations = CurrentViewModel.TempoVariations;
        if (variations == null || variations.Count == 0) return;

        double width = VariationGraphCanvas.ActualWidth;
        double height = VariationGraphCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double detectedBpm = CurrentViewModel.DetectedBpm;
        if (detectedBpm <= 0) return;

        // Find min/max BPM for scaling
        double minBpm = detectedBpm * 0.8;
        double maxBpm = detectedBpm * 1.2;

        double totalTime = 0;
        foreach (var point in variations)
        {
            if (point.Time > totalTime) totalTime = point.Time;
        }

        if (totalTime <= 0) totalTime = 1;

        var points = new PointCollection();

        foreach (var point in variations)
        {
            double x = (point.Time / totalTime) * width;
            double normalizedBpm = (point.Bpm - minBpm) / (maxBpm - minBpm);
            double y = height - normalizedBpm * height;

            points.Add(new Point(
                Math.Clamp(x, 0, width),
                Math.Clamp(y, 0, height)
            ));
        }

        _variationLine.Points = points;

        // Update line color based on stability
        _variationLine.Stroke = new SolidColorBrush(
            CurrentViewModel.HasStableTempo ? AccentColor : WarningColor
        );
    }

    private void ClearGraphs()
    {
        _waveformLine?.Points.Clear();
        _variationLine?.Points.Clear();

        foreach (var line in _beatLines)
        {
            BeatGridCanvas.Children.Remove(line);
        }
        _beatLines.Clear();
    }

    #endregion

    #region Public Methods

    /// <summary>Analyzes audio samples for tempo detection.</summary>
    public void Analyze(float[] samples, int sampleRate = 44100)
    {
        SampleRate = sampleRate;
        AudioSamples = samples;
    }

    /// <summary>Sets the detected BPM manually.</summary>
    public void SetManualBpm(double bpm)
    {
        CurrentViewModel.SetManualBpm(bpm);
    }

    /// <summary>Sets waveform data for visualization.</summary>
    public void SetWaveformData(float[] waveform, double duration)
    {
        WaveformData = waveform;
        AudioDuration = duration;
        CurrentViewModel.SetWaveformData(waveform, duration);
    }

    /// <summary>Resets all detection data.</summary>
    public void Reset()
    {
        CurrentViewModel.ResetCommand.Execute(null);
        ClearGraphs();
    }

    /// <summary>Cancels any ongoing analysis.</summary>
    public void CancelAnalysis()
    {
        CurrentViewModel.CancelAnalysis();
    }

    #endregion
}
