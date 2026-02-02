// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Loop Finder Panel, providing automatic loop detection and editing.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Analysis;

/// <summary>
/// ViewModel for the Loop Finder Panel, handling loop detection, preview,
/// and export functionality.
/// </summary>
public partial class LoopFinderViewModel : ViewModelBase
{
    #region Private Fields

    private float[]? _leftChannelSamples;
    private float[]? _rightChannelSamples;
    private int _sampleRate = 44100;
    private float _bpm = 120f;
    private int _beatsPerBar = 4;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Collection of detected loop candidates.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<LoopCandidateViewModel> _loopCandidates = new();

    /// <summary>
    /// The currently selected loop candidate.
    /// </summary>
    [ObservableProperty]
    private LoopCandidateViewModel? _selectedLoopCandidate;

    /// <summary>
    /// Minimum loop length in bars.
    /// </summary>
    [ObservableProperty]
    private int _minimumLoopLengthBars = 1;

    /// <summary>
    /// Maximum loop length in bars.
    /// </summary>
    [ObservableProperty]
    private int _maximumLoopLengthBars = 8;

    /// <summary>
    /// Similarity threshold for loop detection (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _similarityThreshold = 0.8f;

    /// <summary>
    /// Whether to snap loop points to beats.
    /// </summary>
    [ObservableProperty]
    private bool _snapToBeat = true;

    /// <summary>
    /// Whether to snap loop points to bars.
    /// </summary>
    [ObservableProperty]
    private bool _snapToBar = true;

    /// <summary>
    /// Whether zero-crossing detection is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _zeroCrossingEnabled = true;

    /// <summary>
    /// Crossfade length in milliseconds for seamless looping.
    /// </summary>
    [ObservableProperty]
    private float _crossfadeLengthMs = 10f;

    /// <summary>
    /// Whether loop detection is currently in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isDetecting;

    /// <summary>
    /// Whether the original audio is currently playing (for A/B comparison).
    /// </summary>
    [ObservableProperty]
    private bool _isPlayingOriginal;

    /// <summary>
    /// Whether the looped audio is currently playing (for A/B comparison).
    /// </summary>
    [ObservableProperty]
    private bool _isPlayingLooped;

    /// <summary>
    /// Whether a loop preview is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isPreviewActive;

    /// <summary>
    /// The current playback position in seconds.
    /// </summary>
    [ObservableProperty]
    private double _playbackPosition;

    /// <summary>
    /// Total audio length in seconds.
    /// </summary>
    [ObservableProperty]
    private double _totalLength;

    /// <summary>
    /// Start position of the selected loop in seconds.
    /// </summary>
    [ObservableProperty]
    private double _loopStartPosition;

    /// <summary>
    /// End position of the selected loop in seconds.
    /// </summary>
    [ObservableProperty]
    private double _loopEndPosition;

    /// <summary>
    /// Waveform data for display (normalized samples).
    /// </summary>
    [ObservableProperty]
    private float[]? _waveformData;

    /// <summary>
    /// The current audio file path or name.
    /// </summary>
    [ObservableProperty]
    private string _audioFileName = string.Empty;

    /// <summary>
    /// Progress of the current detection operation (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _detectionProgress;

    /// <summary>
    /// The detected BPM of the audio.
    /// </summary>
    [ObservableProperty]
    private float _detectedBpm = 120f;

    /// <summary>
    /// Whether crossfade preview is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _crossfadePreviewEnabled = true;

    /// <summary>
    /// Current comparison mode (Original or Looped).
    /// </summary>
    [ObservableProperty]
    private ComparisonMode _currentComparisonMode = ComparisonMode.Original;

    #endregion

    #region Events

    /// <summary>
    /// Raised when a loop candidate should be previewed.
    /// </summary>
    public event EventHandler<LoopPreviewEventArgs>? LoopPreviewRequested;

    /// <summary>
    /// Raised when a loop should be exported.
    /// </summary>
    public event EventHandler<LoopExportEventArgs>? LoopExportRequested;

    /// <summary>
    /// Raised when loop markers have changed.
    /// </summary>
    public event EventHandler<LoopMarkersChangedEventArgs>? LoopMarkersChanged;

    /// <summary>
    /// Raised when the comparison mode changes.
    /// </summary>
    public event EventHandler<ComparisonModeChangedEventArgs>? ComparisonModeChanged;

    #endregion

    #region Constructor

    public LoopFinderViewModel()
    {
        // Initialize with default values
    }

    #endregion

    #region Commands

    /// <summary>
    /// Starts automatic loop detection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDetectLoops))]
    private async Task DetectLoopsAsync()
    {
        if (_leftChannelSamples == null || _leftChannelSamples.Length == 0)
            return;

        IsDetecting = true;
        IsBusy = true;
        StatusMessage = "Detecting loops...";
        DetectionProgress = 0f;
        LoopCandidates.Clear();

        try
        {
            await Task.Run(() => PerformLoopDetection());
            StatusMessage = $"Found {LoopCandidates.Count} loop candidate(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Detection failed: {ex.Message}";
        }
        finally
        {
            IsDetecting = false;
            IsBusy = false;
            DetectionProgress = 1f;
        }
    }

    private bool CanDetectLoops() => !IsDetecting && _leftChannelSamples != null && _leftChannelSamples.Length > 0;

    /// <summary>
    /// Previews the selected loop candidate.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPreviewLoop))]
    private void PreviewLoop()
    {
        if (SelectedLoopCandidate == null) return;

        IsPreviewActive = true;
        LoopStartPosition = SelectedLoopCandidate.StartTime;
        LoopEndPosition = SelectedLoopCandidate.EndTime;

        LoopPreviewRequested?.Invoke(this, new LoopPreviewEventArgs(
            SelectedLoopCandidate.StartTime,
            SelectedLoopCandidate.EndTime,
            CrossfadePreviewEnabled ? CrossfadeLengthMs : 0));
    }

    private bool CanPreviewLoop() => SelectedLoopCandidate != null;

    /// <summary>
    /// Stops the current loop preview.
    /// </summary>
    [RelayCommand]
    private void StopPreview()
    {
        IsPreviewActive = false;
        IsPlayingOriginal = false;
        IsPlayingLooped = false;
    }

    /// <summary>
    /// Exports the selected loop as a new audio clip.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportLoop))]
    private void ExportSelectedLoop()
    {
        if (SelectedLoopCandidate == null) return;

        LoopExportRequested?.Invoke(this, new LoopExportEventArgs(
            SelectedLoopCandidate.StartTime,
            SelectedLoopCandidate.EndTime,
            CrossfadeLengthMs,
            ZeroCrossingEnabled));
    }

    private bool CanExportLoop() => SelectedLoopCandidate != null;

    /// <summary>
    /// Exports a loop with the current marker positions.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportCustomLoop))]
    private void ExportCustomLoop()
    {
        if (LoopStartPosition >= LoopEndPosition) return;

        LoopExportRequested?.Invoke(this, new LoopExportEventArgs(
            LoopStartPosition,
            LoopEndPosition,
            CrossfadeLengthMs,
            ZeroCrossingEnabled));
    }

    private bool CanExportCustomLoop() => LoopStartPosition < LoopEndPosition;

    /// <summary>
    /// Plays the original (non-looped) audio for A/B comparison.
    /// </summary>
    [RelayCommand]
    private void PlayOriginal()
    {
        IsPlayingOriginal = true;
        IsPlayingLooped = false;
        CurrentComparisonMode = ComparisonMode.Original;
        ComparisonModeChanged?.Invoke(this, new ComparisonModeChangedEventArgs(ComparisonMode.Original));
    }

    /// <summary>
    /// Plays the looped audio for A/B comparison.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPlayLooped))]
    private void PlayLooped()
    {
        IsPlayingOriginal = false;
        IsPlayingLooped = true;
        CurrentComparisonMode = ComparisonMode.Looped;
        ComparisonModeChanged?.Invoke(this, new ComparisonModeChangedEventArgs(ComparisonMode.Looped));
    }

    private bool CanPlayLooped() => SelectedLoopCandidate != null || LoopStartPosition < LoopEndPosition;

    /// <summary>
    /// Resets the loop detection and clears all candidates.
    /// </summary>
    [RelayCommand]
    private void Reset()
    {
        LoopCandidates.Clear();
        SelectedLoopCandidate = null;
        LoopStartPosition = 0;
        LoopEndPosition = 0;
        DetectionProgress = 0;
        IsPreviewActive = false;
        IsPlayingOriginal = false;
        IsPlayingLooped = false;
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// Sets the loop start marker to the current playback position.
    /// </summary>
    [RelayCommand]
    private void SetLoopStartToPlayhead()
    {
        LoopStartPosition = PlaybackPosition;
        OnLoopMarkersChanged();
    }

    /// <summary>
    /// Sets the loop end marker to the current playback position.
    /// </summary>
    [RelayCommand]
    private void SetLoopEndToPlayhead()
    {
        LoopEndPosition = PlaybackPosition;
        OnLoopMarkersChanged();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Loads audio data for loop analysis.
    /// </summary>
    /// <param name="leftChannel">Left channel samples.</param>
    /// <param name="rightChannel">Right channel samples (can be null for mono).</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <param name="fileName">Name of the audio file.</param>
    public void LoadAudioData(float[] leftChannel, float[]? rightChannel, int sampleRate, string fileName)
    {
        _leftChannelSamples = leftChannel;
        _rightChannelSamples = rightChannel;
        _sampleRate = sampleRate;
        AudioFileName = fileName;
        TotalLength = leftChannel.Length / (double)sampleRate;

        // Generate waveform overview
        GenerateWaveformData();

        DetectLoopsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Sets the tempo information for bar/beat calculations.
    /// </summary>
    /// <param name="bpm">Beats per minute.</param>
    /// <param name="beatsPerBar">Number of beats per bar.</param>
    public void SetTempo(float bpm, int beatsPerBar = 4)
    {
        _bpm = bpm;
        _beatsPerBar = beatsPerBar;
        DetectedBpm = bpm;
    }

    /// <summary>
    /// Updates the loop start position (e.g., from dragging a marker).
    /// </summary>
    /// <param name="position">New start position in seconds.</param>
    public void UpdateLoopStart(double position)
    {
        if (SnapToBar || SnapToBeat)
        {
            position = SnapToGrid(position);
        }

        if (ZeroCrossingEnabled)
        {
            position = FindNearestZeroCrossing(position);
        }

        LoopStartPosition = Math.Max(0, Math.Min(position, LoopEndPosition - 0.001));
        OnLoopMarkersChanged();
    }

    /// <summary>
    /// Updates the loop end position (e.g., from dragging a marker).
    /// </summary>
    /// <param name="position">New end position in seconds.</param>
    public void UpdateLoopEnd(double position)
    {
        if (SnapToBar || SnapToBeat)
        {
            position = SnapToGrid(position);
        }

        if (ZeroCrossingEnabled)
        {
            position = FindNearestZeroCrossing(position);
        }

        LoopEndPosition = Math.Min(TotalLength, Math.Max(position, LoopStartPosition + 0.001));
        OnLoopMarkersChanged();
    }

    /// <summary>
    /// Updates the playback position.
    /// </summary>
    /// <param name="position">Current playback position in seconds.</param>
    public void UpdatePlaybackPosition(double position)
    {
        PlaybackPosition = position;
    }

    #endregion

    #region Private Methods

    private void GenerateWaveformData()
    {
        if (_leftChannelSamples == null || _leftChannelSamples.Length == 0) return;

        // Generate a downsampled waveform for display
        const int targetPoints = 2000;
        int samplesPerPoint = Math.Max(1, _leftChannelSamples.Length / targetPoints);
        int pointCount = _leftChannelSamples.Length / samplesPerPoint;

        var waveform = new float[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            int startSample = i * samplesPerPoint;
            int endSample = Math.Min(startSample + samplesPerPoint, _leftChannelSamples.Length);

            float maxVal = 0;
            for (int j = startSample; j < endSample; j++)
            {
                float absVal = Math.Abs(_leftChannelSamples[j]);
                if (absVal > maxVal) maxVal = absVal;
            }

            // Mix in right channel if stereo
            if (_rightChannelSamples != null && _rightChannelSamples.Length == _leftChannelSamples.Length)
            {
                for (int j = startSample; j < endSample; j++)
                {
                    float absVal = Math.Abs(_rightChannelSamples[j]);
                    if (absVal > maxVal) maxVal = absVal;
                }
            }

            waveform[i] = maxVal;
        }

        WaveformData = waveform;
    }

    private void PerformLoopDetection()
    {
        if (_leftChannelSamples == null) return;

        // Calculate bar and beat durations in samples
        float secondsPerBeat = 60f / _bpm;
        float secondsPerBar = secondsPerBeat * _beatsPerBar;
        int samplesPerBar = (int)(secondsPerBar * _sampleRate);

        int minLoopSamples = samplesPerBar * MinimumLoopLengthBars;
        int maxLoopSamples = samplesPerBar * MaximumLoopLengthBars;

        // Search for potential loop points
        int searchStep = samplesPerBar / 4; // Quarter-bar resolution
        int totalSearches = (_leftChannelSamples.Length - minLoopSamples) / searchStep;
        int searchCount = 0;

        for (int loopLength = minLoopSamples; loopLength <= maxLoopSamples; loopLength += samplesPerBar)
        {
            for (int startSample = 0; startSample < _leftChannelSamples.Length - loopLength; startSample += searchStep)
            {
                float similarity = CalculateLoopSimilarity(startSample, loopLength);

                if (similarity >= SimilarityThreshold)
                {
                    // Found a potential loop
                    double startTime = startSample / (double)_sampleRate;
                    double endTime = (startSample + loopLength) / (double)_sampleRate;
                    double duration = endTime - startTime;

                    int durationBars = (int)Math.Round(duration / secondsPerBar);
                    int durationBeats = (int)Math.Round(duration / secondsPerBeat) % _beatsPerBar;

                    var candidate = new LoopCandidateViewModel
                    {
                        StartTime = startTime,
                        EndTime = endTime,
                        DurationBars = durationBars,
                        DurationBeats = durationBeats,
                        SimilarityScore = similarity,
                        StartSample = startSample,
                        EndSample = startSample + loopLength
                    };

                    // Add to collection on UI thread
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        LoopCandidates.Add(candidate);
                    });
                }

                searchCount++;
                DetectionProgress = (float)searchCount / totalSearches;
            }
        }
    }

    private float CalculateLoopSimilarity(int startSample, int loopLength)
    {
        if (_leftChannelSamples == null) return 0;

        // Compare the end of the loop with the beginning for seamless looping
        int compareLength = Math.Min(loopLength / 8, 4410); // Max 100ms at 44.1kHz

        float sumSquaredDiff = 0;
        float sumSquaredA = 0;

        for (int i = 0; i < compareLength; i++)
        {
            int indexA = startSample + i;
            int indexB = startSample + loopLength - compareLength + i;

            if (indexA >= _leftChannelSamples.Length || indexB >= _leftChannelSamples.Length)
                break;

            float a = _leftChannelSamples[indexA];
            float b = _leftChannelSamples[indexB];

            float diff = a - b;
            sumSquaredDiff += diff * diff;
            sumSquaredA += a * a;
        }

        // Normalized cross-correlation based similarity
        if (sumSquaredA < 0.0001f) return 0; // Silence

        float mse = sumSquaredDiff / compareLength;
        float rms = MathF.Sqrt(sumSquaredA / compareLength);

        // Convert to similarity score (higher is better)
        float similarity = 1f - MathF.Sqrt(mse) / (rms + 0.0001f);
        return Math.Clamp(similarity, 0f, 1f);
    }

    private double SnapToGrid(double position)
    {
        float secondsPerBeat = 60f / _bpm;
        float secondsPerBar = secondsPerBeat * _beatsPerBar;

        if (SnapToBar)
        {
            return Math.Round(position / secondsPerBar) * secondsPerBar;
        }
        else if (SnapToBeat)
        {
            return Math.Round(position / secondsPerBeat) * secondsPerBeat;
        }

        return position;
    }

    private double FindNearestZeroCrossing(double position)
    {
        if (_leftChannelSamples == null) return position;

        int sampleIndex = (int)(position * _sampleRate);
        int searchRadius = (int)(0.005 * _sampleRate); // 5ms search radius

        int bestIndex = sampleIndex;
        float minDist = float.MaxValue;

        for (int i = Math.Max(0, sampleIndex - searchRadius);
             i < Math.Min(_leftChannelSamples.Length - 1, sampleIndex + searchRadius);
             i++)
        {
            // Check for zero crossing
            if ((_leftChannelSamples[i] >= 0 && _leftChannelSamples[i + 1] < 0) ||
                (_leftChannelSamples[i] < 0 && _leftChannelSamples[i + 1] >= 0))
            {
                float dist = Math.Abs(i - sampleIndex);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestIndex = i;
                }
            }
        }

        return bestIndex / (double)_sampleRate;
    }

    private void OnLoopMarkersChanged()
    {
        LoopMarkersChanged?.Invoke(this, new LoopMarkersChangedEventArgs(LoopStartPosition, LoopEndPosition));
    }

    #endregion

    #region Property Changed Handlers

    partial void OnSelectedLoopCandidateChanged(LoopCandidateViewModel? value)
    {
        if (value != null)
        {
            LoopStartPosition = value.StartTime;
            LoopEndPosition = value.EndTime;
            OnLoopMarkersChanged();
        }

        PreviewLoopCommand.NotifyCanExecuteChanged();
        ExportSelectedLoopCommand.NotifyCanExecuteChanged();
        PlayLoopedCommand.NotifyCanExecuteChanged();
    }

    partial void OnLoopStartPositionChanged(double value)
    {
        ExportCustomLoopCommand.NotifyCanExecuteChanged();
        PlayLoopedCommand.NotifyCanExecuteChanged();
    }

    partial void OnLoopEndPositionChanged(double value)
    {
        ExportCustomLoopCommand.NotifyCanExecuteChanged();
        PlayLoopedCommand.NotifyCanExecuteChanged();
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// ViewModel for a detected loop candidate.
/// </summary>
public partial class LoopCandidateViewModel : ObservableObject
{
    [ObservableProperty]
    private double _startTime;

    [ObservableProperty]
    private double _endTime;

    [ObservableProperty]
    private int _durationBars;

    [ObservableProperty]
    private int _durationBeats;

    [ObservableProperty]
    private float _similarityScore;

    [ObservableProperty]
    private int _startSample;

    [ObservableProperty]
    private int _endSample;

    /// <summary>
    /// Formatted display text for the duration.
    /// </summary>
    public string DurationText => DurationBeats > 0
        ? $"{DurationBars} bars + {DurationBeats} beats"
        : $"{DurationBars} bars";

    /// <summary>
    /// Formatted display text for the time range.
    /// </summary>
    public string TimeRangeText => $"{FormatTime(StartTime)} - {FormatTime(EndTime)}";

    /// <summary>
    /// Formatted similarity score as percentage.
    /// </summary>
    public string SimilarityText => $"{SimilarityScore * 100:F1}%";

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}"
            : $"{ts.Seconds}.{ts.Milliseconds / 10:D2}";
    }
}

/// <summary>
/// A/B comparison mode.
/// </summary>
public enum ComparisonMode
{
    Original,
    Looped
}

/// <summary>
/// Event arguments for loop preview requests.
/// </summary>
public class LoopPreviewEventArgs : EventArgs
{
    public double StartTime { get; }
    public double EndTime { get; }
    public float CrossfadeMs { get; }

    public LoopPreviewEventArgs(double startTime, double endTime, float crossfadeMs)
    {
        StartTime = startTime;
        EndTime = endTime;
        CrossfadeMs = crossfadeMs;
    }
}

/// <summary>
/// Event arguments for loop export requests.
/// </summary>
public class LoopExportEventArgs : EventArgs
{
    public double StartTime { get; }
    public double EndTime { get; }
    public float CrossfadeMs { get; }
    public bool UseZeroCrossing { get; }

    public LoopExportEventArgs(double startTime, double endTime, float crossfadeMs, bool useZeroCrossing)
    {
        StartTime = startTime;
        EndTime = endTime;
        CrossfadeMs = crossfadeMs;
        UseZeroCrossing = useZeroCrossing;
    }
}

/// <summary>
/// Event arguments for loop marker changes.
/// </summary>
public class LoopMarkersChangedEventArgs : EventArgs
{
    public double StartPosition { get; }
    public double EndPosition { get; }

    public LoopMarkersChangedEventArgs(double startPosition, double endPosition)
    {
        StartPosition = startPosition;
        EndPosition = endPosition;
    }
}

/// <summary>
/// Event arguments for comparison mode changes.
/// </summary>
public class ComparisonModeChangedEventArgs : EventArgs
{
    public ComparisonMode Mode { get; }

    public ComparisonModeChangedEventArgs(ComparisonMode mode)
    {
        Mode = mode;
    }
}

#endregion
