// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Audio recording clip model.

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents the state of a recording clip.
/// </summary>
public enum RecordingClipState
{
    /// <summary>
    /// Clip is ready to be recorded.
    /// </summary>
    Pending,

    /// <summary>
    /// Clip is currently being recorded.
    /// </summary>
    Recording,

    /// <summary>
    /// Recording is complete and clip is ready for use.
    /// </summary>
    Completed,

    /// <summary>
    /// Recording was cancelled or failed.
    /// </summary>
    Cancelled
}

/// <summary>
/// Represents a recorded audio clip on a track, created during recording sessions.
/// </summary>
public partial class RecordingClip : ObservableObject
{
    private static int _nextId = 1;

    /// <summary>
    /// Gets the unique identifier for this recording clip.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets or sets the track ID this clip belongs to.
    /// </summary>
    [ObservableProperty]
    private int _trackId;

    /// <summary>
    /// Gets or sets the track name for display purposes.
    /// </summary>
    [ObservableProperty]
    private string _trackName = "Track";

    /// <summary>
    /// Gets or sets the start position in beats.
    /// </summary>
    [ObservableProperty]
    private double _startBeat;

    /// <summary>
    /// Gets or sets the duration in beats.
    /// </summary>
    [ObservableProperty]
    private double _duration;

    /// <summary>
    /// Gets the end position in beats.
    /// </summary>
    public double EndBeat => StartBeat + Duration;

    /// <summary>
    /// Gets or sets the start time in seconds.
    /// </summary>
    [ObservableProperty]
    private double _startTime;

    /// <summary>
    /// Gets or sets the duration in seconds.
    /// </summary>
    [ObservableProperty]
    private double _durationSeconds;

    /// <summary>
    /// Gets or sets the file path where the audio is saved.
    /// </summary>
    [ObservableProperty]
    private string? _audioFilePath;

    /// <summary>
    /// Gets or sets the audio buffer containing the recorded samples.
    /// This is used for temporary storage during recording before saving to file.
    /// </summary>
    [ObservableProperty]
    private float[]? _audioBuffer;

    /// <summary>
    /// Gets or sets the waveform data for visual display.
    /// Contains normalized peak values for rendering.
    /// </summary>
    [ObservableProperty]
    private float[]? _waveformData;

    /// <summary>
    /// Gets or sets the waveform data as min/max pairs for detailed display.
    /// </summary>
    [ObservableProperty]
    private WaveformPeakData? _waveformPeaks;

    /// <summary>
    /// Gets or sets the recording state.
    /// </summary>
    [ObservableProperty]
    private RecordingClipState _state = RecordingClipState.Pending;

    /// <summary>
    /// Gets or sets the sample rate of the recording.
    /// </summary>
    [ObservableProperty]
    private int _sampleRate = 44100;

    /// <summary>
    /// Gets or sets the number of channels (1 = mono, 2 = stereo).
    /// </summary>
    [ObservableProperty]
    private int _channels = 2;

    /// <summary>
    /// Gets or sets the bit depth of the recording.
    /// </summary>
    [ObservableProperty]
    private int _bitDepth = 16;

    /// <summary>
    /// Gets or sets the color for visual display (inherited from track).
    /// </summary>
    [ObservableProperty]
    private string _color = "#4A9EFF";

    /// <summary>
    /// Gets or sets the name of this recording clip.
    /// </summary>
    [ObservableProperty]
    private string _name = "Recording";

    /// <summary>
    /// Gets or sets the timestamp when recording started.
    /// </summary>
    [ObservableProperty]
    private DateTime _recordingStartTime;

    /// <summary>
    /// Gets or sets the timestamp when recording ended.
    /// </summary>
    [ObservableProperty]
    private DateTime? _recordingEndTime;

    /// <summary>
    /// Gets or sets whether this clip is selected in the UI.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets whether this clip has audio data.
    /// </summary>
    public bool HasAudioData => AudioBuffer != null && AudioBuffer.Length > 0 || !string.IsNullOrEmpty(AudioFilePath);

    /// <summary>
    /// Gets whether this clip has waveform data for display.
    /// </summary>
    public bool HasWaveformData => WaveformData != null && WaveformData.Length > 0;

    /// <summary>
    /// Gets the formatted duration string.
    /// </summary>
    public string DurationFormatted
    {
        get
        {
            var ts = TimeSpan.FromSeconds(DurationSeconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }
    }

    /// <summary>
    /// Gets the formatted file size (if file exists).
    /// </summary>
    public string FileSizeFormatted
    {
        get
        {
            if (string.IsNullOrEmpty(AudioFilePath) || !System.IO.File.Exists(AudioFilePath))
                return "-";

            var fileInfo = new System.IO.FileInfo(AudioFilePath);
            var bytes = fileInfo.Length;

            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }
    }

    /// <summary>
    /// Creates a new recording clip.
    /// </summary>
    public RecordingClip()
    {
        Id = _nextId++;
        RecordingStartTime = DateTime.Now;
    }

    /// <summary>
    /// Creates a new recording clip for a specific track.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <param name="trackName">The track name.</param>
    /// <param name="startBeat">The start position in beats.</param>
    /// <param name="color">The display color.</param>
    public RecordingClip(int trackId, string trackName, double startBeat, string color) : this()
    {
        TrackId = trackId;
        TrackName = trackName;
        StartBeat = startBeat;
        Color = color;
        Name = $"{trackName} Recording";
    }

    /// <summary>
    /// Updates the duration during recording.
    /// </summary>
    /// <param name="currentBeat">The current beat position.</param>
    /// <param name="currentTime">The current time in seconds.</param>
    public void UpdateDuration(double currentBeat, double currentTime)
    {
        Duration = currentBeat - StartBeat;
        DurationSeconds = currentTime - StartTime;
        OnPropertyChanged(nameof(EndBeat));
        OnPropertyChanged(nameof(DurationFormatted));
    }

    /// <summary>
    /// Marks the recording as complete.
    /// </summary>
    public void CompleteRecording()
    {
        State = RecordingClipState.Completed;
        RecordingEndTime = DateTime.Now;
        OnPropertyChanged(nameof(HasAudioData));
        OnPropertyChanged(nameof(FileSizeFormatted));
    }

    /// <summary>
    /// Marks the recording as cancelled.
    /// </summary>
    public void CancelRecording()
    {
        State = RecordingClipState.Cancelled;
        RecordingEndTime = DateTime.Now;
    }

    /// <summary>
    /// Sets the waveform data from an array of peak values.
    /// </summary>
    /// <param name="peaks">Array of peak values (0.0 to 1.0).</param>
    public void SetWaveformData(float[] peaks)
    {
        WaveformData = peaks;
        OnPropertyChanged(nameof(HasWaveformData));
    }

    /// <summary>
    /// Sets detailed waveform peak data.
    /// </summary>
    /// <param name="peakData">The waveform peak data.</param>
    public void SetWaveformPeaks(WaveformPeakData peakData)
    {
        WaveformPeaks = peakData;
        OnPropertyChanged(nameof(HasWaveformData));
    }

    /// <summary>
    /// Generates simple waveform data from the audio buffer.
    /// </summary>
    /// <param name="samplesPerPeak">Number of samples to combine per peak value.</param>
    public void GenerateWaveformData(int samplesPerPeak = 512)
    {
        if (AudioBuffer == null || AudioBuffer.Length == 0)
            return;

        var peakCount = AudioBuffer.Length / samplesPerPeak;
        if (peakCount < 1) peakCount = 1;

        var peaks = new float[peakCount];

        for (int i = 0; i < peakCount; i++)
        {
            float maxPeak = 0;
            int start = i * samplesPerPeak;
            int end = Math.Min(start + samplesPerPeak, AudioBuffer.Length);

            for (int j = start; j < end; j++)
            {
                var abs = Math.Abs(AudioBuffer[j]);
                if (abs > maxPeak) maxPeak = abs;
            }

            peaks[i] = maxPeak;
        }

        WaveformData = peaks;
        OnPropertyChanged(nameof(HasWaveformData));
    }

    /// <summary>
    /// Clears the in-memory audio buffer to free memory (after saving to file).
    /// </summary>
    public void ClearAudioBuffer()
    {
        AudioBuffer = null;
    }
}

/// <summary>
/// Holds detailed waveform peak data with min/max values for high-quality rendering.
/// </summary>
public class WaveformPeakData
{
    /// <summary>
    /// Gets or sets the minimum peak values per segment.
    /// </summary>
    public float[] MinPeaks { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum peak values per segment.
    /// </summary>
    public float[] MaxPeaks { get; set; } = [];

    /// <summary>
    /// Gets or sets the RMS (average) values per segment.
    /// </summary>
    public float[] RmsPeaks { get; set; } = [];

    /// <summary>
    /// Gets the number of peak segments.
    /// </summary>
    public int SegmentCount => MinPeaks?.Length ?? 0;

    /// <summary>
    /// Gets or sets the number of samples per segment.
    /// </summary>
    public int SamplesPerSegment { get; set; } = 512;

    /// <summary>
    /// Gets or sets the total number of samples represented.
    /// </summary>
    public int TotalSamples { get; set; }
}

/// <summary>
/// Represents a track that is armed for recording with its associated state.
/// </summary>
public partial class ArmedTrackInfo : ObservableObject
{
    /// <summary>
    /// Gets or sets the track ID.
    /// </summary>
    [ObservableProperty]
    private int _trackId;

    /// <summary>
    /// Gets or sets the track name.
    /// </summary>
    [ObservableProperty]
    private string _trackName = "Track";

    /// <summary>
    /// Gets or sets the track color.
    /// </summary>
    [ObservableProperty]
    private string _color = "#4A9EFF";

    /// <summary>
    /// Gets or sets the input source identifier.
    /// </summary>
    [ObservableProperty]
    private string? _inputSource;

    /// <summary>
    /// Gets or sets the input source display name.
    /// </summary>
    [ObservableProperty]
    private string _inputSourceName = "Default Input";

    /// <summary>
    /// Gets or sets the current input level (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _inputLevel;

    /// <summary>
    /// Gets or sets the peak input level in dB.
    /// </summary>
    [ObservableProperty]
    private float _inputLevelDb = -60f;

    /// <summary>
    /// Gets or sets whether the input is clipping.
    /// </summary>
    [ObservableProperty]
    private bool _isClipping;

    /// <summary>
    /// Gets or sets whether input monitoring is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isMonitoring;

    /// <summary>
    /// Gets or sets the current recording clip (if recording).
    /// </summary>
    [ObservableProperty]
    private RecordingClip? _currentClip;

    /// <summary>
    /// Creates a new armed track info.
    /// </summary>
    public ArmedTrackInfo()
    {
    }

    /// <summary>
    /// Creates a new armed track info from track details.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <param name="trackName">The track name.</param>
    /// <param name="color">The track color.</param>
    /// <param name="inputSource">The input source ID.</param>
    /// <param name="inputSourceName">The input source name.</param>
    public ArmedTrackInfo(int trackId, string trackName, string color, string? inputSource, string inputSourceName)
    {
        TrackId = trackId;
        TrackName = trackName;
        Color = color;
        InputSource = inputSource;
        InputSourceName = inputSourceName;
    }

    /// <summary>
    /// Updates the input level from a linear value.
    /// </summary>
    /// <param name="level">Linear level (0.0 to 1.0+).</param>
    public void UpdateInputLevel(float level)
    {
        InputLevel = level;
        InputLevelDb = level <= 0 ? -60f : (float)(20.0 * Math.Log10(level));
        IsClipping = level >= 0.99f;
    }

    /// <summary>
    /// Resets the peak indicators.
    /// </summary>
    public void ResetPeaks()
    {
        InputLevel = 0;
        InputLevelDb = -60f;
        IsClipping = false;
    }
}
