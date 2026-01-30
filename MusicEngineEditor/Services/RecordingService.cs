// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Audio/MIDI recording service.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MusicEngine.Core;
using MusicEngineEditor.Models;
using NAudio.Wave;

namespace MusicEngineEditor.Services;

/// <summary>
/// Event arguments for recording events.
/// </summary>
public class RecordingEventArgs : EventArgs
{
    /// <summary>
    /// Gets the recording start time in beats.
    /// </summary>
    public double StartBeat { get; }

    /// <summary>
    /// Gets the recording start time in seconds.
    /// </summary>
    public double StartTime { get; }

    /// <summary>
    /// Gets the armed tracks being recorded.
    /// </summary>
    public IReadOnlyList<ArmedTrackInfo> ArmedTracks { get; }

    /// <summary>
    /// Creates new recording event args.
    /// </summary>
    public RecordingEventArgs(double startBeat, double startTime, IReadOnlyList<ArmedTrackInfo> armedTracks)
    {
        StartBeat = startBeat;
        StartTime = startTime;
        ArmedTracks = armedTracks;
    }
}

/// <summary>
/// Event arguments for recording stopped.
/// </summary>
public class RecordingStoppedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the recorded clips.
    /// </summary>
    public IReadOnlyList<RecordingClip> RecordedClips { get; }

    /// <summary>
    /// Gets the total recording duration in beats.
    /// </summary>
    public double DurationBeats { get; }

    /// <summary>
    /// Gets the total recording duration in seconds.
    /// </summary>
    public double DurationSeconds { get; }

    /// <summary>
    /// Gets whether recording was cancelled.
    /// </summary>
    public bool WasCancelled { get; }

    /// <summary>
    /// Creates new recording stopped event args.
    /// </summary>
    public RecordingStoppedEventArgs(IReadOnlyList<RecordingClip> clips, double durationBeats, double durationSeconds, bool wasCancelled)
    {
        RecordedClips = clips;
        DurationBeats = durationBeats;
        DurationSeconds = durationSeconds;
        WasCancelled = wasCancelled;
    }
}

/// <summary>
/// Event arguments for track arm/disarm events.
/// </summary>
public class TrackArmEventArgs : EventArgs
{
    /// <summary>
    /// Gets the track ID.
    /// </summary>
    public int TrackId { get; }

    /// <summary>
    /// Gets the track name.
    /// </summary>
    public string TrackName { get; }

    /// <summary>
    /// Gets whether the track is armed.
    /// </summary>
    public bool IsArmed { get; }

    /// <summary>
    /// Creates new track arm event args.
    /// </summary>
    public TrackArmEventArgs(int trackId, string trackName, bool isArmed)
    {
        TrackId = trackId;
        TrackName = trackName;
        IsArmed = isArmed;
    }
}

/// <summary>
/// Service for managing multi-track audio recording.
/// Supports recording to multiple armed tracks simultaneously with synchronized start times.
/// </summary>
public sealed class RecordingService : IDisposable
{
    #region Singleton

    private static readonly Lazy<RecordingService> _instance = new(
        () => new RecordingService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the RecordingService.
    /// </summary>
    public static RecordingService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private readonly object _lock = new();
    private readonly Dictionary<int, ArmedTrackInfo> _armedTracks = new();
    private readonly Dictionary<int, WaveFileWriter?> _trackWriters = new();
    private readonly Dictionary<int, List<float>> _trackBuffers = new();
    private readonly ConcurrentDictionary<int, RecordingClip> _activeClips = new();
    private readonly List<RecordingClip> _completedClips = new();

    private WaveInEvent? _waveIn;
    private MetronomeService? _metronomeService;
    private PlaybackService? _playbackService;
    private DispatcherTimer? _updateTimer;
    private CancellationTokenSource? _recordingCts;

    private bool _isRecording;
    private bool _isCountingIn;
    private int _countInBarsRemaining;
    private double _recordingStartBeat;
    private double _recordingStartTime;
    private DateTime _recordingStartTimestamp;
    private string _projectRecordingsPath = "";
    private bool _disposed;

    // Recording settings
    private int _sampleRate = 44100;
    private int _bitDepth = 16;
    private int _channels = 2;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the collection of armed tracks.
    /// </summary>
    public IReadOnlyDictionary<int, ArmedTrackInfo> ArmedTracks => _armedTracks;

    /// <summary>
    /// Gets the list of armed track infos.
    /// </summary>
    public IEnumerable<ArmedTrackInfo> ArmedTracksList => _armedTracks.Values;

    /// <summary>
    /// Gets the count of armed tracks.
    /// </summary>
    public int ArmedTrackCount => _armedTracks.Count;

    /// <summary>
    /// Gets whether any tracks are armed for recording.
    /// </summary>
    public bool HasArmedTracks => _armedTracks.Count > 0;

    /// <summary>
    /// Gets whether recording is currently active.
    /// </summary>
    public bool IsRecording => _isRecording;

    /// <summary>
    /// Gets whether count-in is active.
    /// </summary>
    public bool IsCountingIn => _isCountingIn;

    /// <summary>
    /// Gets the remaining count-in bars.
    /// </summary>
    public int CountInBarsRemaining => _countInBarsRemaining;

    /// <summary>
    /// Gets the current recording duration in beats.
    /// </summary>
    public double RecordingDurationBeats
    {
        get
        {
            if (!_isRecording || _playbackService == null) return 0;
            return _playbackService.CurrentBeat - _recordingStartBeat;
        }
    }

    /// <summary>
    /// Gets the current recording duration in seconds.
    /// </summary>
    public double RecordingDurationSeconds
    {
        get
        {
            if (!_isRecording) return 0;
            return (DateTime.Now - _recordingStartTimestamp).TotalSeconds;
        }
    }

    /// <summary>
    /// Gets the formatted recording duration.
    /// </summary>
    public string RecordingDurationFormatted
    {
        get
        {
            var ts = TimeSpan.FromSeconds(RecordingDurationSeconds);
            return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
        }
    }

    /// <summary>
    /// Gets the active recording clips.
    /// </summary>
    public IReadOnlyDictionary<int, RecordingClip> ActiveClips => _activeClips;

    /// <summary>
    /// Gets the completed recording clips from the last session.
    /// </summary>
    public IReadOnlyList<RecordingClip> CompletedClips => _completedClips;

    /// <summary>
    /// Gets or sets the number of count-in bars (0, 1, 2, or 4).
    /// </summary>
    public int CountInBars { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether click track is enabled during recording.
    /// </summary>
    public bool ClickTrackEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the recording sample rate.
    /// </summary>
    public int SampleRate
    {
        get => _sampleRate;
        set => _sampleRate = value > 0 ? value : 44100;
    }

    /// <summary>
    /// Gets or sets the recording bit depth.
    /// </summary>
    public int BitDepth
    {
        get => _bitDepth;
        set => _bitDepth = value is 8 or 16 or 24 or 32 ? value : 16;
    }

    /// <summary>
    /// Gets or sets the number of recording channels.
    /// </summary>
    public int Channels
    {
        get => _channels;
        set => _channels = value is 1 or 2 ? value : 2;
    }

    /// <summary>
    /// Gets or sets the project recordings folder path.
    /// </summary>
    public string ProjectRecordingsPath
    {
        get => _projectRecordingsPath;
        set => _projectRecordingsPath = value ?? "";
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when recording starts.
    /// </summary>
    public event EventHandler<RecordingEventArgs>? RecordingStarted;

    /// <summary>
    /// Raised when recording stops.
    /// </summary>
    public event EventHandler<RecordingStoppedEventArgs>? RecordingStopped;

    /// <summary>
    /// Raised when count-in starts.
    /// </summary>
    public event EventHandler<int>? CountInStarted;

    /// <summary>
    /// Raised on each count-in beat.
    /// </summary>
    public event EventHandler<int>? CountInBeat;

    /// <summary>
    /// Raised when a track is armed.
    /// </summary>
    public event EventHandler<TrackArmEventArgs>? TrackArmed;

    /// <summary>
    /// Raised when a track is disarmed.
    /// </summary>
    public event EventHandler<TrackArmEventArgs>? TrackDisarmed;

    /// <summary>
    /// Raised when input levels are updated.
    /// </summary>
    public event EventHandler? InputLevelsUpdated;

    /// <summary>
    /// Raised when recording state changes.
    /// </summary>
    public event EventHandler<bool>? RecordingStateChanged;

    #endregion

    #region Constructor

    private RecordingService()
    {
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // 20fps for level meters
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        // Set default recordings path
        _projectRecordingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            "MusicEngine",
            "Recordings");
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the recording service with required dependencies.
    /// </summary>
    /// <param name="playbackService">The playback service for synchronization.</param>
    /// <param name="metronomeService">The metronome service for count-in and click.</param>
    public void Initialize(PlaybackService playbackService, MetronomeService? metronomeService = null)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _metronomeService = metronomeService;
    }

    /// <summary>
    /// Sets the project recordings folder path.
    /// </summary>
    /// <param name="projectPath">The project folder path.</param>
    public void SetProjectPath(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            _projectRecordingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                "MusicEngine",
                "Recordings");
        }
        else
        {
            _projectRecordingsPath = Path.Combine(projectPath, "Recordings");
        }

        // Ensure directory exists
        Directory.CreateDirectory(_projectRecordingsPath);
    }

    #endregion

    #region Track Arming

    /// <summary>
    /// Arms a track for recording.
    /// </summary>
    /// <param name="trackId">The track ID to arm.</param>
    /// <param name="trackName">The track name.</param>
    /// <param name="color">The track color.</param>
    /// <param name="inputSource">The input source identifier.</param>
    /// <param name="inputSourceName">The input source display name.</param>
    public void ArmTrack(int trackId, string trackName, string color, string? inputSource, string inputSourceName)
    {
        ThrowIfDisposed();

        if (_isRecording)
        {
            throw new InvalidOperationException("Cannot arm tracks while recording is in progress.");
        }

        lock (_lock)
        {
            if (!_armedTracks.ContainsKey(trackId))
            {
                var armedTrack = new ArmedTrackInfo(trackId, trackName, color, inputSource, inputSourceName);
                _armedTracks[trackId] = armedTrack;

                TrackArmed?.Invoke(this, new TrackArmEventArgs(trackId, trackName, true));

                // Start input monitoring if this is the first armed track
                if (_armedTracks.Count == 1)
                {
                    StartInputMonitoring();
                }
            }
        }
    }

    /// <summary>
    /// Arms a track using TrackInfo.
    /// </summary>
    /// <param name="track">The track info.</param>
    public void ArmTrack(TrackInfo track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        ArmTrack(track.Id, track.Name, track.Color, track.InputSource, track.InputSourceName);
    }

    /// <summary>
    /// Disarms a track from recording.
    /// </summary>
    /// <param name="trackId">The track ID to disarm.</param>
    public void DisarmTrack(int trackId)
    {
        ThrowIfDisposed();

        if (_isRecording)
        {
            throw new InvalidOperationException("Cannot disarm tracks while recording is in progress.");
        }

        lock (_lock)
        {
            if (_armedTracks.TryGetValue(trackId, out var armedTrack))
            {
                _armedTracks.Remove(trackId);

                TrackDisarmed?.Invoke(this, new TrackArmEventArgs(trackId, armedTrack.TrackName, false));

                // Stop input monitoring if no more armed tracks
                if (_armedTracks.Count == 0)
                {
                    StopInputMonitoring();
                }
            }
        }
    }

    /// <summary>
    /// Toggles the armed state of a track.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <param name="trackName">The track name.</param>
    /// <param name="color">The track color.</param>
    /// <param name="inputSource">The input source.</param>
    /// <param name="inputSourceName">The input source name.</param>
    /// <returns>True if the track is now armed.</returns>
    public bool ToggleArmTrack(int trackId, string trackName, string color, string? inputSource, string inputSourceName)
    {
        if (IsTrackArmed(trackId))
        {
            DisarmTrack(trackId);
            return false;
        }
        else
        {
            ArmTrack(trackId, trackName, color, inputSource, inputSourceName);
            return true;
        }
    }

    /// <summary>
    /// Checks if a track is armed for recording.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <returns>True if the track is armed.</returns>
    public bool IsTrackArmed(int trackId)
    {
        lock (_lock)
        {
            return _armedTracks.ContainsKey(trackId);
        }
    }

    /// <summary>
    /// Disarms all tracks.
    /// </summary>
    public void DisarmAllTracks()
    {
        ThrowIfDisposed();

        if (_isRecording)
        {
            throw new InvalidOperationException("Cannot disarm tracks while recording is in progress.");
        }

        lock (_lock)
        {
            var trackIds = _armedTracks.Keys.ToList();
            foreach (var trackId in trackIds)
            {
                DisarmTrack(trackId);
            }
        }
    }

    #endregion

    #region Recording Control

    /// <summary>
    /// Starts recording on all armed tracks.
    /// </summary>
    /// <param name="useCountIn">Whether to use count-in before recording.</param>
    public async Task StartRecordingAsync(bool useCountIn = true)
    {
        ThrowIfDisposed();

        if (_isRecording)
        {
            throw new InvalidOperationException("Recording is already in progress.");
        }

        if (!HasArmedTracks)
        {
            throw new InvalidOperationException("No tracks are armed for recording.");
        }

        _recordingCts = new CancellationTokenSource();

        try
        {
            // Ensure recordings directory exists
            Directory.CreateDirectory(_projectRecordingsPath);

            // Count-in if enabled
            if (useCountIn && CountInBars > 0)
            {
                await PerformCountInAsync(_recordingCts.Token);

                if (_recordingCts.IsCancellationRequested)
                {
                    return;
                }
            }

            // Start actual recording
            StartRecordingInternal();
        }
        catch (OperationCanceledException)
        {
            // Count-in was cancelled
        }
    }

    /// <summary>
    /// Starts recording synchronously without count-in.
    /// </summary>
    public void StartRecording()
    {
        ThrowIfDisposed();

        if (_isRecording)
        {
            throw new InvalidOperationException("Recording is already in progress.");
        }

        if (!HasArmedTracks)
        {
            throw new InvalidOperationException("No tracks are armed for recording.");
        }

        // Ensure recordings directory exists
        Directory.CreateDirectory(_projectRecordingsPath);

        StartRecordingInternal();
    }

    /// <summary>
    /// Stops recording on all tracks.
    /// </summary>
    /// <param name="cancel">If true, discards recorded data.</param>
    public void StopRecording(bool cancel = false)
    {
        ThrowIfDisposed();

        if (_isCountingIn)
        {
            _recordingCts?.Cancel();
            _isCountingIn = false;
            _countInBarsRemaining = 0;
            RecordingStateChanged?.Invoke(this, false);
            return;
        }

        if (!_isRecording)
        {
            return;
        }

        lock (_lock)
        {
            _isRecording = false;

            var durationBeats = RecordingDurationBeats;
            var durationSeconds = RecordingDurationSeconds;

            // Stop audio input
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
            }

            // Finalize all clips
            _completedClips.Clear();

            foreach (var kvp in _activeClips)
            {
                var clip = kvp.Value;

                if (cancel)
                {
                    clip.CancelRecording();
                }
                else
                {
                    // Write any remaining buffer data
                    if (_trackBuffers.TryGetValue(kvp.Key, out var buffer) && buffer.Count > 0)
                    {
                        WriteBufferToFile(kvp.Key, buffer);
                    }

                    clip.UpdateDuration(_playbackService?.CurrentBeat ?? 0, _playbackService?.CurrentTime ?? 0);
                    clip.CompleteRecording();

                    // Generate waveform data
                    clip.GenerateWaveformData();

                    _completedClips.Add(clip);
                }
            }

            // Close all file writers
            foreach (var writer in _trackWriters.Values)
            {
                writer?.Dispose();
            }
            _trackWriters.Clear();
            _trackBuffers.Clear();
            _activeClips.Clear();

            RecordingStateChanged?.Invoke(this, false);
            RecordingStopped?.Invoke(this, new RecordingStoppedEventArgs(
                _completedClips.AsReadOnly(),
                durationBeats,
                durationSeconds,
                cancel));

            // Stop playback if it was auto-started
            _playbackService?.Stop();
        }
    }

    /// <summary>
    /// Cancels the current recording, discarding all data.
    /// </summary>
    public void CancelRecording()
    {
        StopRecording(cancel: true);
    }

    #endregion

    #region Private Recording Methods

    private async Task PerformCountInAsync(CancellationToken cancellationToken)
    {
        _isCountingIn = true;
        _countInBarsRemaining = CountInBars;

        CountInStarted?.Invoke(this, CountInBars);
        RecordingStateChanged?.Invoke(this, true);

        // Enable click if requested
        var wasMetronomeEnabled = _metronomeService?.IsEnabled ?? false;
        if (ClickTrackEnabled && _metronomeService != null)
        {
            _metronomeService.IsEnabled = true;
        }

        var bpm = _playbackService?.BPM ?? 120;
        var beatsPerBar = _metronomeService?.BeatsPerBar ?? 4;
        var beatDuration = TimeSpan.FromMilliseconds(60000.0 / bpm);

        for (int bar = 0; bar < CountInBars && !cancellationToken.IsCancellationRequested; bar++)
        {
            _countInBarsRemaining = CountInBars - bar;

            for (int beat = 0; beat < beatsPerBar && !cancellationToken.IsCancellationRequested; beat++)
            {
                CountInBeat?.Invoke(this, beat + 1);

                try
                {
                    await Task.Delay(beatDuration, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _isCountingIn = false;
        _countInBarsRemaining = 0;

        // Restore metronome state if we changed it
        if (!ClickTrackEnabled && _metronomeService != null)
        {
            _metronomeService.IsEnabled = wasMetronomeEnabled;
        }
    }

    private void StartRecordingInternal()
    {
        lock (_lock)
        {
            _recordingStartBeat = _playbackService?.CurrentBeat ?? 0;
            _recordingStartTime = _playbackService?.CurrentTime ?? 0;
            _recordingStartTimestamp = DateTime.Now;

            // Create recording clips and file writers for each armed track
            foreach (var kvp in _armedTracks)
            {
                var trackId = kvp.Key;
                var armedTrack = kvp.Value;

                // Create recording clip
                var clip = new RecordingClip(trackId, armedTrack.TrackName, _recordingStartBeat, armedTrack.Color)
                {
                    StartTime = _recordingStartTime,
                    SampleRate = _sampleRate,
                    Channels = _channels,
                    BitDepth = _bitDepth,
                    State = RecordingClipState.Recording
                };

                // Generate unique filename
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var safeName = string.Join("_", armedTrack.TrackName.Split(Path.GetInvalidFileNameChars()));
                var fileName = $"{safeName}_{timestamp}.wav";
                var filePath = Path.Combine(_projectRecordingsPath, fileName);

                clip.AudioFilePath = filePath;

                // Create wave file writer
                var waveFormat = new WaveFormat(_sampleRate, _bitDepth, _channels);
                var writer = new WaveFileWriter(filePath, waveFormat);
                _trackWriters[trackId] = writer;
                _trackBuffers[trackId] = new List<float>();

                // Set current clip on armed track
                armedTrack.CurrentClip = clip;
                _activeClips[trackId] = clip;
            }

            _isRecording = true;

            // Start playback if not already playing
            if (_playbackService != null && !_playbackService.IsPlaying)
            {
                _playbackService.Play();
            }

            // Enable click track if configured
            if (ClickTrackEnabled && _metronomeService != null)
            {
                _metronomeService.IsEnabled = true;
            }

            RecordingStateChanged?.Invoke(this, true);
            RecordingStarted?.Invoke(this, new RecordingEventArgs(
                _recordingStartBeat,
                _recordingStartTime,
                _armedTracks.Values.ToList()));
        }
    }

    private void StartInputMonitoring()
    {
        try
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(_sampleRate, _bitDepth, _channels),
                BufferMilliseconds = 50
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            _updateTimer?.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start input monitoring: {ex.Message}");
        }
    }

    private void StopInputMonitoring()
    {
        _updateTimer?.Stop();

        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isRecording) return;

        // Convert bytes to floats
        var bytesPerSample = _bitDepth / 8;
        var sampleCount = e.BytesRecorded / bytesPerSample;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            if (_bitDepth == 16)
            {
                var sample = BitConverter.ToInt16(e.Buffer, i * 2);
                samples[i] = sample / 32768f;
            }
            else if (_bitDepth == 24)
            {
                var b1 = e.Buffer[i * 3];
                var b2 = e.Buffer[i * 3 + 1];
                var b3 = e.Buffer[i * 3 + 2];
                var sample = (b3 << 16) | (b2 << 8) | b1;
                if ((sample & 0x800000) != 0) sample |= unchecked((int)0xFF000000);
                samples[i] = sample / 8388608f;
            }
            else if (_bitDepth == 32)
            {
                samples[i] = BitConverter.ToSingle(e.Buffer, i * 4);
            }
        }

        // Calculate peak levels
        float peak = 0;
        foreach (var sample in samples)
        {
            var abs = Math.Abs(sample);
            if (abs > peak) peak = abs;
        }

        // Update levels on all armed tracks
        foreach (var armedTrack in _armedTracks.Values)
        {
            armedTrack.UpdateInputLevel(peak);
        }

        // Write to all track buffers/files (for now, same data to all tracks)
        // In a real implementation, each track might have a different input source
        lock (_lock)
        {
            foreach (var trackId in _armedTracks.Keys)
            {
                if (_trackBuffers.TryGetValue(trackId, out var buffer))
                {
                    buffer.AddRange(samples);

                    // Also update the clip's audio buffer for waveform display
                    if (_activeClips.TryGetValue(trackId, out var clip))
                    {
                        var existingBuffer = clip.AudioBuffer ?? [];
                        var newBuffer = new float[existingBuffer.Length + samples.Length];
                        Array.Copy(existingBuffer, newBuffer, existingBuffer.Length);
                        Array.Copy(samples, 0, newBuffer, existingBuffer.Length, samples.Length);
                        clip.AudioBuffer = newBuffer;
                    }

                    // Write to file periodically (every ~1 second)
                    if (buffer.Count >= _sampleRate * _channels)
                    {
                        WriteBufferToFile(trackId, buffer);
                        buffer.Clear();
                    }
                }
            }
        }
    }

    private void WriteBufferToFile(int trackId, List<float> buffer)
    {
        if (!_trackWriters.TryGetValue(trackId, out var writer) || writer == null)
            return;

        // Convert floats to bytes based on bit depth
        byte[] bytes;

        if (_bitDepth == 16)
        {
            bytes = new byte[buffer.Count * 2];
            for (int i = 0; i < buffer.Count; i++)
            {
                var sample = (short)(buffer[i] * 32767);
                BitConverter.GetBytes(sample).CopyTo(bytes, i * 2);
            }
        }
        else if (_bitDepth == 24)
        {
            bytes = new byte[buffer.Count * 3];
            for (int i = 0; i < buffer.Count; i++)
            {
                var sample = (int)(buffer[i] * 8388607);
                bytes[i * 3] = (byte)sample;
                bytes[i * 3 + 1] = (byte)(sample >> 8);
                bytes[i * 3 + 2] = (byte)(sample >> 16);
            }
        }
        else // 32-bit float
        {
            bytes = new byte[buffer.Count * 4];
            for (int i = 0; i < buffer.Count; i++)
            {
                BitConverter.GetBytes(buffer[i]).CopyTo(bytes, i * 4);
            }
        }

        writer.Write(bytes, 0, bytes.Length);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            System.Diagnostics.Debug.WriteLine($"Recording stopped with error: {e.Exception.Message}");
        }
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        // Update recording clips duration
        if (_isRecording && _playbackService != null)
        {
            foreach (var clip in _activeClips.Values)
            {
                clip.UpdateDuration(_playbackService.CurrentBeat, _playbackService.CurrentTime);
            }
        }

        InputLevelsUpdated?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the armed track info for a track ID.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <returns>The armed track info or null if not armed.</returns>
    public ArmedTrackInfo? GetArmedTrack(int trackId)
    {
        lock (_lock)
        {
            return _armedTracks.TryGetValue(trackId, out var track) ? track : null;
        }
    }

    /// <summary>
    /// Gets the recording clip for a track.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <returns>The active recording clip or null.</returns>
    public RecordingClip? GetActiveClip(int trackId)
    {
        return _activeClips.TryGetValue(trackId, out var clip) ? clip : null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RecordingService));
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the recording service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop any active recording
        if (_isRecording)
        {
            StopRecording(cancel: true);
        }

        // Stop input monitoring
        StopInputMonitoring();

        _recordingCts?.Cancel();
        _recordingCts?.Dispose();

        _updateTimer?.Stop();

        // Dispose writers
        foreach (var writer in _trackWriters.Values)
        {
            writer?.Dispose();
        }
        _trackWriters.Clear();

        _armedTracks.Clear();
        _trackBuffers.Clear();
        _activeClips.Clear();
        _completedClips.Clear();
    }

    #endregion
}
