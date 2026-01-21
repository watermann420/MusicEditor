// MusicEngineEditor - Playback Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MusicEngine.Core;

namespace MusicEngineEditor.Services;

/// <summary>
/// Singleton service managing audio playback state and synchronization.
/// Integrates with MusicEngine.Core.Sequencer and AudioEngine.
/// </summary>
public sealed class PlaybackService : IDisposable
{
    #region Singleton

    private static readonly Lazy<PlaybackService> _instance = new(
        () => new PlaybackService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the PlaybackService.
    /// </summary>
    public static PlaybackService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private Sequencer? _sequencer;
    private AudioEngine? _audioEngine;
    private DispatcherTimer? _positionTimer;
    private readonly object _lock = new();
    private bool _disposed;

    private bool _isPlaying;
    private bool _isPaused;
    private bool _isScrubbing;
    private double _currentBeat;
    private double _currentTime;
    private double _bpm = 120.0;
    private bool _loopEnabled;
    private double _loopStart;
    private double _loopEnd = 16.0;
    private double _pausedAtBeat;
    private bool _wasPlayingBeforeScrub;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether playback is currently active.
    /// </summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged(nameof(IsPlaying));
            }
        }
    }

    /// <summary>
    /// Gets whether playback is paused.
    /// </summary>
    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (_isPaused != value)
            {
                _isPaused = value;
                OnPropertyChanged(nameof(IsPaused));
            }
        }
    }

    /// <summary>
    /// Gets whether scrub mode is currently active.
    /// </summary>
    public bool IsScrubbing
    {
        get => _isScrubbing;
        private set
        {
            if (_isScrubbing != value)
            {
                _isScrubbing = value;
                OnPropertyChanged(nameof(IsScrubbing));
            }
        }
    }

    /// <summary>
    /// Gets the current playback position in beats.
    /// </summary>
    public double CurrentBeat
    {
        get => _currentBeat;
        private set
        {
            if (Math.Abs(_currentBeat - value) > 0.001)
            {
                _currentBeat = value;
                OnPropertyChanged(nameof(CurrentBeat));
            }
        }
    }

    /// <summary>
    /// Gets the current playback time in seconds.
    /// </summary>
    public double CurrentTime
    {
        get => _currentTime;
        private set
        {
            if (Math.Abs(_currentTime - value) > 0.001)
            {
                _currentTime = value;
                OnPropertyChanged(nameof(CurrentTime));
            }
        }
    }

    /// <summary>
    /// Gets or sets the tempo in beats per minute.
    /// </summary>
    public double BPM
    {
        get => _bpm;
        set
        {
            if (Math.Abs(_bpm - value) > 0.001 && value > 0 && value <= 999)
            {
                var oldBpm = _bpm;
                _bpm = value;

                if (_sequencer != null)
                {
                    _sequencer.Bpm = value;
                }

                OnPropertyChanged(nameof(BPM));
                BpmChanged?.Invoke(this, new BpmChangedEventArgs(value, oldBpm));
                EventBus.Instance.PublishBpmChanged(value, oldBpm);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether loop playback is enabled.
    /// </summary>
    public bool LoopEnabled
    {
        get => _loopEnabled;
        set
        {
            if (_loopEnabled != value)
            {
                _loopEnabled = value;
                OnPropertyChanged(nameof(LoopEnabled));
                LoopStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets or sets the loop start position in beats.
    /// </summary>
    public double LoopStart
    {
        get => _loopStart;
        set
        {
            if (Math.Abs(_loopStart - value) > 0.001 && value >= 0)
            {
                _loopStart = value;
                OnPropertyChanged(nameof(LoopStart));
                LoopRegionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets or sets the loop end position in beats.
    /// </summary>
    public double LoopEnd
    {
        get => _loopEnd;
        set
        {
            if (Math.Abs(_loopEnd - value) > 0.001 && value > _loopStart)
            {
                _loopEnd = value;
                OnPropertyChanged(nameof(LoopEnd));
                LoopRegionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets whether the service has been initialized with engine references.
    /// </summary>
    public bool IsInitialized => _sequencer != null && _audioEngine != null;

    /// <summary>
    /// Gets the connected sequencer instance.
    /// </summary>
    public Sequencer? Sequencer => _sequencer;

    /// <summary>
    /// Gets the connected audio engine instance.
    /// </summary>
    public AudioEngine? AudioEngine => _audioEngine;

    #endregion

    #region Events

    /// <summary>
    /// Raised when playback starts.
    /// </summary>
    public event EventHandler<PlaybackStartedEventArgs>? PlaybackStarted;

    /// <summary>
    /// Raised when playback stops.
    /// </summary>
    public event EventHandler<PlaybackStoppedEventArgs>? PlaybackStopped;

    /// <summary>
    /// Raised when playback is paused.
    /// </summary>
    public event EventHandler? PlaybackPaused;

    /// <summary>
    /// Raised when playback resumes from pause.
    /// </summary>
    public event EventHandler? PlaybackResumed;

    /// <summary>
    /// Raised when the playback position changes.
    /// </summary>
    public event EventHandler<PositionChangedEventArgs>? PositionChanged;

    /// <summary>
    /// Raised when the BPM changes.
    /// </summary>
    public event EventHandler<BpmChangedEventArgs>? BpmChanged;

    /// <summary>
    /// Raised when the loop region changes.
    /// </summary>
    public event EventHandler? LoopRegionChanged;

    /// <summary>
    /// Raised when the loop enabled state changes.
    /// </summary>
    public event EventHandler? LoopStateChanged;

    /// <summary>
    /// Raised when a property value changes.
    /// </summary>
    public event EventHandler<string>? PropertyChanged;

    #endregion

    #region Constructor

    private PlaybackService()
    {
        // Initialize position update timer (30 fps for smooth playhead movement)
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 30.0) // ~33ms for 30fps
        };
        _positionTimer.Tick += OnPositionTimerTick;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the playback service with engine references.
    /// </summary>
    /// <param name="audioEngine">The audio engine instance.</param>
    /// <param name="sequencer">The sequencer instance.</param>
    public void Initialize(AudioEngine audioEngine, Sequencer sequencer)
    {
        lock (_lock)
        {
            _audioEngine = audioEngine ?? throw new ArgumentNullException(nameof(audioEngine));
            _sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));

            // Sync BPM from sequencer
            _bpm = _sequencer.Bpm;

            // Subscribe to sequencer events if available
            // The sequencer may raise events when patterns trigger notes, etc.
        }
    }

    /// <summary>
    /// Initializes with an existing EngineService.
    /// </summary>
    /// <param name="engineService">The engine service containing audio engine and sequencer.</param>
    public void Initialize(EngineService engineService)
    {
        if (engineService == null)
            throw new ArgumentNullException(nameof(engineService));

        if (!engineService.IsInitialized)
            throw new InvalidOperationException("EngineService must be initialized first.");

        // Get the sequencer from the engine service
        var sequencer = engineService.Sequencer;
        if (sequencer == null)
            throw new InvalidOperationException("EngineService sequencer is not available.");

        // For now, we don't have direct access to AudioEngine from EngineService
        // We'll work primarily with the Sequencer
        lock (_lock)
        {
            _sequencer = sequencer;
            _bpm = _sequencer.Bpm;
        }
    }

    #endregion

    #region Playback Control

    /// <summary>
    /// Starts or resumes playback.
    /// </summary>
    public void Play()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_sequencer == null)
            {
                throw new InvalidOperationException("PlaybackService is not initialized. Call Initialize() first.");
            }

            if (IsPaused)
            {
                // Resume from paused position
                Resume();
                return;
            }

            if (IsPlaying)
            {
                return; // Already playing
            }

            // Start sequencer
            _sequencer.Start();
            IsPlaying = true;
            IsPaused = false;

            // Start position update timer
            _positionTimer?.Start();

            // Raise events
            var args = new PlaybackStartedEventArgs(CurrentBeat, BPM);
            PlaybackStarted?.Invoke(this, args);
            EventBus.Instance.PublishPlaybackStarted(CurrentBeat, BPM);
        }
    }

    /// <summary>
    /// Pauses playback at the current position.
    /// </summary>
    public void Pause()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!IsPlaying || IsPaused)
            {
                return;
            }

            _pausedAtBeat = CurrentBeat;
            _sequencer?.Stop();
            IsPlaying = false;
            IsPaused = true;

            // Stop position updates
            _positionTimer?.Stop();

            // Raise event
            PlaybackPaused?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Resumes playback from a paused state.
    /// </summary>
    public void Resume()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!IsPaused)
            {
                return;
            }

            // Resume from paused position
            _sequencer?.Start();
            IsPlaying = true;
            IsPaused = false;

            // Restart position updates
            _positionTimer?.Start();

            // Raise event
            PlaybackResumed?.Invoke(this, EventArgs.Empty);
            EventBus.Instance.PublishPlaybackStarted(_pausedAtBeat, BPM);
        }
    }

    /// <summary>
    /// Stops playback and resets position to the beginning.
    /// </summary>
    public void Stop()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var stoppedAtBeat = CurrentBeat;

            _sequencer?.Stop();
            IsPlaying = false;
            IsPaused = false;

            // Stop position updates
            _positionTimer?.Stop();

            // Reset position
            CurrentBeat = 0;
            CurrentTime = 0;
            _pausedAtBeat = 0;

            // Raise events
            var args = new PlaybackStoppedEventArgs(stoppedAtBeat, PlaybackStopReason.UserRequested);
            PlaybackStopped?.Invoke(this, args);
            EventBus.Instance.PublishPlaybackStopped(stoppedAtBeat);

            // Notify position change
            PositionChanged?.Invoke(this, new PositionChangedEventArgs(0, 0));
        }
    }

    /// <summary>
    /// Toggles between play and pause states.
    /// </summary>
    public void TogglePlayPause()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    /// <summary>
    /// Sets the playback position to the specified beat.
    /// </summary>
    /// <param name="beat">The beat position to seek to.</param>
    public void SetPosition(double beat)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (beat < 0)
            {
                beat = 0;
            }

            CurrentBeat = beat;
            CurrentTime = BeatToTime(beat);
            _pausedAtBeat = beat;

            // Notify position change
            PositionChanged?.Invoke(this, new PositionChangedEventArgs(beat, CurrentTime));
            EventBus.Instance.PublishBeatChanged(beat);
        }
    }

    /// <summary>
    /// Toggles loop playback.
    /// </summary>
    public void ToggleLoop()
    {
        LoopEnabled = !LoopEnabled;
    }

    /// <summary>
    /// Sets the loop region.
    /// </summary>
    /// <param name="start">Loop start position in beats.</param>
    /// <param name="end">Loop end position in beats.</param>
    public void SetLoopRegion(double start, double end)
    {
        if (start >= 0 && end > start)
        {
            LoopStart = start;
            LoopEnd = end;
        }
    }

    /// <summary>
    /// Jumps to the beginning of the loop region or the start.
    /// </summary>
    public void JumpToStart()
    {
        SetPosition(LoopEnabled ? LoopStart : 0);
    }

    /// <summary>
    /// Jumps to the end of the loop region.
    /// </summary>
    public void JumpToEnd()
    {
        SetPosition(LoopEnd);
    }

    #endregion

    #region Scrub Mode

    /// <summary>
    /// Enters scrub mode, pausing normal playback if active.
    /// </summary>
    public void EnterScrubMode()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (IsScrubbing)
            {
                return;
            }

            // Remember if we were playing before scrub
            _wasPlayingBeforeScrub = IsPlaying && !IsPaused;

            // Pause playback if playing
            if (IsPlaying)
            {
                _sequencer?.Stop();
                _positionTimer?.Stop();
                IsPlaying = false;
            }

            IsScrubbing = true;

            // Raise event
            ScrubModeEntered?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Exits scrub mode and optionally resumes playback.
    /// </summary>
    public void ExitScrubMode()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!IsScrubbing)
            {
                return;
            }

            IsScrubbing = false;

            // Raise event
            ScrubModeExited?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Updates the position during scrubbing.
    /// This method is optimized for frequent calls during drag operations.
    /// </summary>
    /// <param name="beat">The beat position to scrub to.</param>
    public void ScrubTo(double beat)
    {
        ThrowIfDisposed();

        if (!IsScrubbing)
        {
            // Allow scrubbing even if not in explicit scrub mode
            // This enables simpler usage patterns
        }

        lock (_lock)
        {
            if (beat < 0)
            {
                beat = 0;
            }

            // Update position without triggering full playback logic
            CurrentBeat = beat;
            CurrentTime = BeatToTime(beat);

            // Notify position change (throttled for performance)
            PositionChanged?.Invoke(this, new PositionChangedEventArgs(beat, CurrentTime));

            // Use a lighter-weight event for scrub updates
            ScrubPositionChanged?.Invoke(this, new ScrubPositionChangedEventArgs(beat, CurrentTime));
        }
    }

    /// <summary>
    /// Raised when scrub mode is entered.
    /// </summary>
    public event EventHandler? ScrubModeEntered;

    /// <summary>
    /// Raised when scrub mode is exited.
    /// </summary>
    public event EventHandler? ScrubModeExited;

    /// <summary>
    /// Raised when the scrub position changes (lightweight event for frequent updates).
    /// </summary>
    public event EventHandler<ScrubPositionChangedEventArgs>? ScrubPositionChanged;

    #endregion

    #region Time Conversion

    /// <summary>
    /// Converts beats to time in seconds.
    /// </summary>
    /// <param name="beats">The beat position.</param>
    /// <returns>The time in seconds.</returns>
    public double BeatToTime(double beats)
    {
        return (beats / BPM) * 60.0;
    }

    /// <summary>
    /// Converts time in seconds to beats.
    /// </summary>
    /// <param name="seconds">The time in seconds.</param>
    /// <returns>The beat position.</returns>
    public double TimeToBeat(double seconds)
    {
        return (seconds * BPM) / 60.0;
    }

    #endregion

    #region Timer Callback

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying || IsPaused)
        {
            return;
        }

        // Update current position from sequencer
        if (_sequencer != null)
        {
            var newBeat = _sequencer.CurrentBeat;

            // Handle looping
            if (LoopEnabled && newBeat >= LoopEnd)
            {
                SetPosition(LoopStart);
                return;
            }

            CurrentBeat = newBeat;
            CurrentTime = BeatToTime(newBeat);

            // Raise position changed event
            PositionChanged?.Invoke(this, new PositionChangedEventArgs(newBeat, CurrentTime));
            EventBus.Instance.PublishBeatChanged(newBeat);
        }
    }

    #endregion

    #region Helper Methods

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, propertyName);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PlaybackService));
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the PlaybackService.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _positionTimer?.Stop();
            _positionTimer = null;

            // Don't dispose engine/sequencer as they're owned elsewhere
            _sequencer = null;
            _audioEngine = null;
        }
    }

    #endregion
}

#region Event Args Classes

/// <summary>
/// Event arguments for PlaybackStarted event.
/// </summary>
public sealed class PlaybackStartedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the beat position where playback started.
    /// </summary>
    public double StartBeat { get; }

    /// <summary>
    /// Gets the BPM at playback start.
    /// </summary>
    public double Bpm { get; }

    /// <summary>
    /// Gets the timestamp when playback started.
    /// </summary>
    public DateTime Timestamp { get; }

    public PlaybackStartedEventArgs(double startBeat, double bpm)
    {
        StartBeat = startBeat;
        Bpm = bpm;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for PlaybackStopped event.
/// </summary>
public sealed class PlaybackStoppedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the beat position where playback stopped.
    /// </summary>
    public double StoppedAtBeat { get; }

    /// <summary>
    /// Gets the reason playback stopped.
    /// </summary>
    public PlaybackStopReason Reason { get; }

    /// <summary>
    /// Gets the timestamp when playback stopped.
    /// </summary>
    public DateTime Timestamp { get; }

    public PlaybackStoppedEventArgs(double stoppedAtBeat, PlaybackStopReason reason)
    {
        StoppedAtBeat = stoppedAtBeat;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Reasons for playback stopping.
/// </summary>
public enum PlaybackStopReason
{
    /// <summary>
    /// User requested stop.
    /// </summary>
    UserRequested,

    /// <summary>
    /// Reached end of sequence.
    /// </summary>
    EndOfSequence,

    /// <summary>
    /// An error occurred.
    /// </summary>
    Error
}

/// <summary>
/// Event arguments for PositionChanged event.
/// </summary>
public sealed class PositionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the current beat position.
    /// </summary>
    public double Beat { get; }

    /// <summary>
    /// Gets the current time in seconds.
    /// </summary>
    public double Time { get; }

    /// <summary>
    /// Gets the timestamp when the position changed.
    /// </summary>
    public DateTime Timestamp { get; }

    public PositionChangedEventArgs(double beat, double time)
    {
        Beat = beat;
        Time = time;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for BpmChanged event.
/// </summary>
public sealed class BpmChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the new BPM value.
    /// </summary>
    public double NewBpm { get; }

    /// <summary>
    /// Gets the previous BPM value.
    /// </summary>
    public double OldBpm { get; }

    /// <summary>
    /// Gets the timestamp when the BPM changed.
    /// </summary>
    public DateTime Timestamp { get; }

    public BpmChangedEventArgs(double newBpm, double oldBpm)
    {
        NewBpm = newBpm;
        OldBpm = oldBpm;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for ScrubPositionChanged event.
/// Lightweight event args for frequent scrub updates.
/// </summary>
public sealed class ScrubPositionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the current scrub position in beats.
    /// </summary>
    public double Beat { get; }

    /// <summary>
    /// Gets the current scrub position in seconds.
    /// </summary>
    public double Time { get; }

    public ScrubPositionChangedEventArgs(double beat, double time)
    {
        Beat = beat;
        Time = time;
    }
}

#endregion
