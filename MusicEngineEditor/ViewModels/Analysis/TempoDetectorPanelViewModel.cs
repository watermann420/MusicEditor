// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for TempoDetectorPanel with comprehensive tempo analysis.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Analysis;

/// <summary>
/// ViewModel for tempo detection panel providing BPM analysis, tap tempo,
/// beat grid visualization, time signature detection, and tempo variation analysis.
/// </summary>
public partial class TempoDetectorPanelViewModel : ViewModelBase
{
    #region Constants

    private const double TapResetTimeoutSeconds = 3.0;
    private const double DefaultMinBpm = 60.0;
    private const double DefaultMaxBpm = 200.0;
    private const int MaxTapHistory = 16;

    #endregion

    #region Observable Properties

    [ObservableProperty]
    private double _detectedBpm;

    [ObservableProperty]
    private double _tapTempoBpm;

    [ObservableProperty]
    private double _confidence;

    [ObservableProperty]
    private string _confidenceLevel = "Low";

    [ObservableProperty]
    private int _tapCount;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _canApplyTempo;

    [ObservableProperty]
    private string _detectedTimeSignature = "4/4";

    [ObservableProperty]
    private int _beatsPerMeasure = 4;

    [ObservableProperty]
    private int _beatUnit = 4;

    [ObservableProperty]
    private double _halfTimeBpm;

    [ObservableProperty]
    private double _doubleTimeBpm;

    [ObservableProperty]
    private bool _isMetronomeSyncEnabled;

    [ObservableProperty]
    private double _minBpm = DefaultMinBpm;

    [ObservableProperty]
    private double _maxBpm = DefaultMaxBpm;

    [ObservableProperty]
    private double _onsetSensitivity = 0.5;

    [ObservableProperty]
    private double _downbeatConfidence;

    [ObservableProperty]
    private bool _isDownbeatDetected;

    [ObservableProperty]
    private double _audioDuration;

    [ObservableProperty]
    private ObservableCollection<double> _beatPositions = new();

    [ObservableProperty]
    private ObservableCollection<TempoVariationPoint> _tempoVariations = new();

    [ObservableProperty]
    private float[]? _waveformData;

    [ObservableProperty]
    private double _averageTempoVariation;

    [ObservableProperty]
    private bool _hasStableTempo;

    #endregion

    #region Private Fields

    private CancellationTokenSource? _analysisCts;
    private readonly List<DateTime> _tapTimes = new();
    private DateTime _lastTapTime = DateTime.MinValue;

    #endregion

    #region Events

    /// <summary>Event raised when tempo should be applied to the project.</summary>
    public event EventHandler<TempoApplyEventArgs>? ApplyTempoRequested;

    /// <summary>Event raised when analysis completes.</summary>
    public event EventHandler<TempoAnalysisCompletedEventArgs>? AnalysisCompleted;

    /// <summary>Event raised when metronome sync state changes.</summary>
    public event EventHandler<bool>? MetronomeSyncChanged;

    #endregion

    #region Constructor

    public TempoDetectorPanelViewModel()
    {
        UpdateAlternativeTempos();
    }

    #endregion

    #region Computed Properties

    /// <summary>Gets the BPM display text.</summary>
    public string BpmDisplayText => DetectedBpm > 0 ? DetectedBpm.ToString("F1") : "---";

    /// <summary>Gets the tap tempo display text.</summary>
    public string TapBpmDisplayText => TapTempoBpm > 0 ? TapTempoBpm.ToString("F1") : "---";

    /// <summary>Gets the confidence percentage (0-100).</summary>
    public double ConfidencePercent => Confidence * 100;

    /// <summary>Gets the tap count display text.</summary>
    public string TapCountText => $"{TapCount} taps";

    /// <summary>Gets the downbeat confidence percentage.</summary>
    public double DownbeatConfidencePercent => DownbeatConfidence * 100;

    #endregion

    #region Property Changed Handlers

    partial void OnDetectedBpmChanged(double value)
    {
        CanApplyTempo = value > 0;
        UpdateAlternativeTempos();
        OnPropertyChanged(nameof(BpmDisplayText));
    }

    partial void OnTapTempoBpmChanged(double value)
    {
        OnPropertyChanged(nameof(TapBpmDisplayText));
    }

    partial void OnConfidenceChanged(double value)
    {
        UpdateConfidenceLevel();
        OnPropertyChanged(nameof(ConfidencePercent));
    }

    partial void OnDownbeatConfidenceChanged(double value)
    {
        OnPropertyChanged(nameof(DownbeatConfidencePercent));
        IsDownbeatDetected = value >= 0.5;
    }

    partial void OnTapCountChanged(int value)
    {
        OnPropertyChanged(nameof(TapCountText));
    }

    partial void OnIsMetronomeSyncEnabledChanged(bool value)
    {
        MetronomeSyncChanged?.Invoke(this, value);
    }

    partial void OnBeatsPerMeasureChanged(int value)
    {
        UpdateTimeSignatureDisplay();
    }

    partial void OnBeatUnitChanged(int value)
    {
        UpdateTimeSignatureDisplay();
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void Tap()
    {
        var now = DateTime.UtcNow;

        // Check for tap timeout - reset if too long since last tap
        if ((now - _lastTapTime).TotalSeconds > TapResetTimeoutSeconds && _tapTimes.Count > 0)
        {
            _tapTimes.Clear();
        }

        _tapTimes.Add(now);
        _lastTapTime = now;

        // Keep only recent taps
        while (_tapTimes.Count > MaxTapHistory)
        {
            _tapTimes.RemoveAt(0);
        }

        TapCount = _tapTimes.Count;

        // Calculate BPM from tap intervals
        if (_tapTimes.Count >= 2)
        {
            CalculateTapTempo();
        }
    }

    [RelayCommand]
    private void ResetTapTempo()
    {
        _tapTimes.Clear();
        TapCount = 0;
        TapTempoBpm = 0;
    }

    [RelayCommand]
    private void UseTapTempo()
    {
        if (TapTempoBpm > 0)
        {
            DetectedBpm = TapTempoBpm;
            Confidence = Math.Min(1.0, TapCount / 8.0);
            UpdateConfidenceLevel();
        }
    }

    [RelayCommand]
    private void UseHalfTime()
    {
        if (HalfTimeBpm > 0 && HalfTimeBpm >= MinBpm)
        {
            DetectedBpm = HalfTimeBpm;
        }
    }

    [RelayCommand]
    private void UseDoubleTime()
    {
        if (DoubleTimeBpm > 0 && DoubleTimeBpm <= MaxBpm)
        {
            DetectedBpm = DoubleTimeBpm;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyTempo))]
    private void ApplyTempo()
    {
        if (DetectedBpm > 0)
        {
            ApplyTempoRequested?.Invoke(this, new TempoApplyEventArgs
            {
                Bpm = DetectedBpm,
                TimeSignatureNumerator = BeatsPerMeasure,
                TimeSignatureDenominator = BeatUnit,
                BeatPositions = new List<double>(BeatPositions),
                SyncMetronome = IsMetronomeSyncEnabled
            });
        }
    }

    [RelayCommand]
    private async Task AnalyzeAsync(float[]? samples)
    {
        if (samples == null || samples.Length == 0)
            return;

        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();

        IsAnalyzing = true;
        IsBusy = true;
        StatusMessage = "Analyzing tempo...";

        try
        {
            await Task.Run(() => PerformAnalysis(samples, 44100, _analysisCts.Token), _analysisCts.Token);
            StatusMessage = HasStableTempo ? "Detection complete" : "Variable tempo detected";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Analysis cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _analysisCts?.Cancel();

        DetectedBpm = 0;
        TapTempoBpm = 0;
        Confidence = 0;
        TapCount = 0;
        DownbeatConfidence = 0;
        IsDownbeatDetected = false;
        BeatPositions.Clear();
        TempoVariations.Clear();
        WaveformData = null;
        AudioDuration = 0;
        AverageTempoVariation = 0;
        HasStableTempo = false;
        DetectedTimeSignature = "4/4";
        BeatsPerMeasure = 4;
        BeatUnit = 4;
        CanApplyTempo = false;
        StatusMessage = "Ready";

        _tapTimes.Clear();
    }

    [RelayCommand]
    private void SetTimeSignature(string signature)
    {
        switch (signature)
        {
            case "4/4":
                BeatsPerMeasure = 4;
                BeatUnit = 4;
                break;
            case "3/4":
                BeatsPerMeasure = 3;
                BeatUnit = 4;
                break;
            case "6/8":
                BeatsPerMeasure = 6;
                BeatUnit = 8;
                break;
            case "2/4":
                BeatsPerMeasure = 2;
                BeatUnit = 4;
                break;
            case "5/4":
                BeatsPerMeasure = 5;
                BeatUnit = 4;
                break;
            case "7/8":
                BeatsPerMeasure = 7;
                BeatUnit = 8;
                break;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>Analyzes audio samples for tempo detection.</summary>
    public void Analyze(float[] samples, int sampleRate = 44100)
    {
        _ = AnalyzeAsync(samples);
    }

    /// <summary>Sets the detected BPM manually.</summary>
    public void SetManualBpm(double bpm)
    {
        if (bpm > 0)
        {
            DetectedBpm = Math.Clamp(bpm, MinBpm, MaxBpm);
            Confidence = 1.0;
            UpdateConfidenceLevel();
        }
    }

    /// <summary>Sets waveform data for visualization.</summary>
    public void SetWaveformData(float[] waveform, double duration)
    {
        WaveformData = waveform;
        AudioDuration = duration;
    }

    /// <summary>Cancels any ongoing analysis.</summary>
    public void CancelAnalysis()
    {
        _analysisCts?.Cancel();
    }

    #endregion

    #region Private Methods

    private void CalculateTapTempo()
    {
        if (_tapTimes.Count < 2) return;

        double totalMs = 0;
        int intervals = 0;

        for (int i = 1; i < _tapTimes.Count; i++)
        {
            double intervalMs = (_tapTimes[i] - _tapTimes[i - 1]).TotalMilliseconds;

            // Filter out unreasonable intervals
            if (intervalMs > 200 && intervalMs < 2000)
            {
                totalMs += intervalMs;
                intervals++;
            }
        }

        if (intervals > 0)
        {
            double avgIntervalMs = totalMs / intervals;
            double bpm = 60000.0 / avgIntervalMs;

            // Clamp to reasonable range
            TapTempoBpm = Math.Clamp(bpm, 30, 300);
        }
    }

    private void PerformAnalysis(float[] samples, int sampleRate, CancellationToken cancellationToken)
    {
        // Simplified tempo detection algorithm
        // In a real implementation, this would use FFT-based onset detection
        // and autocorrelation for tempo estimation

        int windowSize = sampleRate / 10; // 100ms windows
        var onsets = new List<int>();
        var energyHistory = new List<double>();

        // Calculate energy in windows
        for (int i = 0; i < samples.Length - windowSize; i += windowSize / 4)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double energy = 0;
            for (int j = 0; j < windowSize && i + j < samples.Length; j++)
            {
                energy += samples[i + j] * samples[i + j];
            }
            energy = Math.Sqrt(energy / windowSize);
            energyHistory.Add(energy);
        }

        // Find onsets (significant energy increases)
        double threshold = OnsetSensitivity * 0.5;
        for (int i = 1; i < energyHistory.Count; i++)
        {
            double diff = energyHistory[i] - energyHistory[i - 1];
            if (diff > threshold && energyHistory[i] > 0.01)
            {
                int samplePos = i * (windowSize / 4);
                onsets.Add(samplePos);
            }
        }

        if (onsets.Count < 4)
        {
            // Not enough onsets for tempo detection
            return;
        }

        // Calculate inter-onset intervals
        var intervals = new List<double>();
        for (int i = 1; i < onsets.Count; i++)
        {
            double intervalSec = (onsets[i] - onsets[i - 1]) / (double)sampleRate;
            if (intervalSec > 0.2 && intervalSec < 2.0)
            {
                intervals.Add(intervalSec);
            }
        }

        if (intervals.Count < 2) return;

        // Find most common interval (histogram approach)
        var histogram = new Dictionary<int, int>();
        foreach (double interval in intervals)
        {
            int binMs = (int)(interval * 1000 / 10) * 10; // 10ms bins
            if (!histogram.ContainsKey(binMs))
                histogram[binMs] = 0;
            histogram[binMs]++;
        }

        // Find peak
        int maxCount = 0;
        int peakBinMs = 500;
        foreach (var kvp in histogram)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                peakBinMs = kvp.Key;
            }
        }

        double beatIntervalSec = peakBinMs / 1000.0;
        double detectedBpm = 60.0 / beatIntervalSec;

        // Clamp to filter range
        while (detectedBpm < MinBpm && detectedBpm > 0)
            detectedBpm *= 2;
        while (detectedBpm > MaxBpm)
            detectedBpm /= 2;

        // Update on UI thread
        App.Current?.Dispatcher?.Invoke(() =>
        {
            DetectedBpm = Math.Round(detectedBpm, 1);
            Confidence = Math.Min(1.0, maxCount / (double)intervals.Count);
            AudioDuration = samples.Length / (double)sampleRate;

            // Generate beat positions
            BeatPositions.Clear();
            double beatInterval = 60.0 / DetectedBpm;
            for (double pos = 0; pos < AudioDuration; pos += beatInterval)
            {
                BeatPositions.Add(pos);
            }

            // Calculate tempo variations
            CalculateTempoVariations(intervals, beatIntervalSec);

            // Detect time signature (simplified)
            DetectTimeSignature(intervals, beatIntervalSec);

            // Downbeat detection (simplified)
            DownbeatConfidence = Confidence * 0.8;

            AnalysisCompleted?.Invoke(this, new TempoAnalysisCompletedEventArgs
            {
                Bpm = DetectedBpm,
                Confidence = Confidence,
                BeatPositions = new List<double>(BeatPositions),
                TimeSignature = DetectedTimeSignature
            });
        });
    }

    private void CalculateTempoVariations(List<double> intervals, double expectedInterval)
    {
        TempoVariations.Clear();

        if (intervals.Count < 2) return;

        double sumVariation = 0;
        int count = 0;
        double time = 0;

        foreach (double interval in intervals)
        {
            double instantBpm = 60.0 / interval;
            double variation = Math.Abs(interval - expectedInterval) / expectedInterval;
            sumVariation += variation;
            count++;

            TempoVariations.Add(new TempoVariationPoint
            {
                Time = time,
                Bpm = instantBpm,
                Deviation = variation * 100
            });

            time += interval;
        }

        AverageTempoVariation = count > 0 ? (sumVariation / count) * 100 : 0;
        HasStableTempo = AverageTempoVariation < 5; // Less than 5% variation
    }

    private void DetectTimeSignature(List<double> intervals, double beatInterval)
    {
        // Simplified time signature detection
        // Look for patterns in interval groupings

        // Default to 4/4
        BeatsPerMeasure = 4;
        BeatUnit = 4;

        // Check for waltz-like patterns (groups of 3)
        int threeGroups = 0;
        int fourGroups = 0;

        for (int i = 2; i < intervals.Count; i++)
        {
            double sum3 = intervals[i] + intervals[i - 1] + intervals[i - 2];
            double sum4 = i >= 3 ? intervals[i] + intervals[i - 1] + intervals[i - 2] + intervals[i - 3] : 0;

            double expected3 = beatInterval * 3;
            double expected4 = beatInterval * 4;

            if (Math.Abs(sum3 - expected3) < expected3 * 0.1)
                threeGroups++;
            if (i >= 3 && Math.Abs(sum4 - expected4) < expected4 * 0.1)
                fourGroups++;
        }

        if (threeGroups > fourGroups * 1.5)
        {
            BeatsPerMeasure = 3;
            BeatUnit = 4;
        }

        UpdateTimeSignatureDisplay();
    }

    private void UpdateTimeSignatureDisplay()
    {
        DetectedTimeSignature = $"{BeatsPerMeasure}/{BeatUnit}";
    }

    private void UpdateConfidenceLevel()
    {
        ConfidenceLevel = Confidence switch
        {
            >= 0.8 => "High",
            >= 0.5 => "Medium",
            _ => "Low"
        };
    }

    private void UpdateAlternativeTempos()
    {
        HalfTimeBpm = DetectedBpm > 0 ? DetectedBpm / 2 : 0;
        DoubleTimeBpm = DetectedBpm > 0 ? DetectedBpm * 2 : 0;
    }

    #endregion
}

#region Supporting Types

/// <summary>Represents a point in the tempo variation graph.</summary>
public class TempoVariationPoint
{
    public double Time { get; set; }
    public double Bpm { get; set; }
    public double Deviation { get; set; }
}

/// <summary>Event args for tempo apply request.</summary>
public class TempoApplyEventArgs : EventArgs
{
    public double Bpm { get; set; }
    public int TimeSignatureNumerator { get; set; }
    public int TimeSignatureDenominator { get; set; }
    public List<double> BeatPositions { get; set; } = new();
    public bool SyncMetronome { get; set; }
}

/// <summary>Event args for analysis completion.</summary>
public class TempoAnalysisCompletedEventArgs : EventArgs
{
    public double Bpm { get; set; }
    public double Confidence { get; set; }
    public List<double> BeatPositions { get; set; } = new();
    public string TimeSignature { get; set; } = "4/4";
}

#endregion
