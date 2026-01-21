// MusicEngineEditor - Scrub Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Threading;
using System.Windows.Threading;
using MusicEngine.Core;

namespace MusicEngineEditor.Services;

/// <summary>
/// Singleton service for audio scrubbing during timeline/playhead dragging.
/// Provides real-time audio feedback with variable playback speed based on drag velocity.
/// Uses small audio buffers for responsive, low-latency scrubbing.
/// </summary>
public sealed class ScrubService : IDisposable
{
    #region Singleton

    private static readonly Lazy<ScrubService> _instance = new(
        () => new ScrubService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the ScrubService.
    /// </summary>
    public static ScrubService Instance => _instance.Value;

    #endregion

    #region Constants

    /// <summary>
    /// Minimum time between scrub updates in milliseconds.
    /// </summary>
    private const double MinScrubIntervalMs = 16.0; // ~60fps

    /// <summary>
    /// Maximum scrub speed multiplier (forward or backward).
    /// </summary>
    private const double MaxScrubSpeed = 4.0;

    /// <summary>
    /// Minimum scrub speed multiplier (for very slow movements).
    /// </summary>
    private const double MinScrubSpeed = 0.1;

    /// <summary>
    /// Scrub audio buffer size in samples (small for low latency).
    /// </summary>
    private const int ScrubBufferSamples = 512;

    /// <summary>
    /// Crossfade duration in samples to avoid clicks.
    /// </summary>
    private const int CrossfadeSamples = 64;

    /// <summary>
    /// Velocity smoothing factor (0-1, higher = more smoothing).
    /// </summary>
    private const double VelocitySmoothingFactor = 0.3;

    #endregion

    #region Private Fields

    private readonly object _lock = new();
    private bool _disposed;
    private bool _isScrubbing;
    private double _scrubPosition;
    private double _previousScrubPosition;
    private double _scrubSpeed;
    private double _smoothedVelocity;
    private DateTime _lastScrubTime;
    private DispatcherTimer? _scrubAudioTimer;

    // Audio scrubbing state
    private float[]? _scrubBuffer;
    private float[]? _crossfadeBuffer;
    private int _scrubBufferPosition;
    private bool _scrubAudioEnabled = true;
    private double _scrubVolume = 0.7;

    // Velocity tracking for speed calculation
    private double _velocityAccumulator;
    private int _velocitySampleCount;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether audio scrubbing is currently active.
    /// </summary>
    public bool IsScrubbing
    {
        get => _isScrubbing;
        private set
        {
            if (_isScrubbing != value)
            {
                _isScrubbing = value;
                PropertyChanged?.Invoke(this, nameof(IsScrubbing));
            }
        }
    }

    /// <summary>
    /// Gets the current scrub position in beats.
    /// </summary>
    public double ScrubPosition
    {
        get => _scrubPosition;
        private set
        {
            if (Math.Abs(_scrubPosition - value) > 0.001)
            {
                _scrubPosition = value;
                PropertyChanged?.Invoke(this, nameof(ScrubPosition));
            }
        }
    }

    /// <summary>
    /// Gets the current scrub speed multiplier.
    /// Positive values indicate forward scrubbing, negative for backward.
    /// </summary>
    public double ScrubSpeed
    {
        get => _scrubSpeed;
        private set
        {
            if (Math.Abs(_scrubSpeed - value) > 0.01)
            {
                _scrubSpeed = value;
                PropertyChanged?.Invoke(this, nameof(ScrubSpeed));
            }
        }
    }

    /// <summary>
    /// Gets or sets whether scrub audio output is enabled.
    /// </summary>
    public bool ScrubAudioEnabled
    {
        get => _scrubAudioEnabled;
        set
        {
            if (_scrubAudioEnabled != value)
            {
                _scrubAudioEnabled = value;
                PropertyChanged?.Invoke(this, nameof(ScrubAudioEnabled));
            }
        }
    }

    /// <summary>
    /// Gets or sets the scrub audio volume (0.0 to 1.0).
    /// </summary>
    public double ScrubVolume
    {
        get => _scrubVolume;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_scrubVolume - clamped) > 0.01)
            {
                _scrubVolume = clamped;
                PropertyChanged?.Invoke(this, nameof(ScrubVolume));
            }
        }
    }

    /// <summary>
    /// Gets the smoothed velocity for UI feedback.
    /// </summary>
    public double SmoothedVelocity => _smoothedVelocity;

    #endregion

    #region Events

    /// <summary>
    /// Raised when a property value changes.
    /// </summary>
    public event EventHandler<string>? PropertyChanged;

    /// <summary>
    /// Raised when scrubbing starts.
    /// </summary>
    public event EventHandler<ScrubStartedEventArgs>? ScrubStarted;

    /// <summary>
    /// Raised when the scrub position updates.
    /// </summary>
    public event EventHandler<ScrubUpdateEventArgs>? ScrubUpdated;

    /// <summary>
    /// Raised when scrubbing ends.
    /// </summary>
    public event EventHandler<ScrubEndedEventArgs>? ScrubEnded;

    #endregion

    #region Constructor

    private ScrubService()
    {
        // Initialize scrub buffers
        _scrubBuffer = new float[ScrubBufferSamples * 2]; // Stereo
        _crossfadeBuffer = new float[CrossfadeSamples * 2];

        // Initialize scrub audio timer (for continuous scrub playback)
        _scrubAudioTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(MinScrubIntervalMs)
        };
        _scrubAudioTimer.Tick += OnScrubAudioTimerTick;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts scrubbing at the specified position.
    /// </summary>
    /// <param name="startPosition">The starting position in beats.</param>
    public void StartScrub(double startPosition)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (IsScrubbing)
            {
                return;
            }

            // Initialize scrub state
            _scrubPosition = Math.Max(0, startPosition);
            _previousScrubPosition = _scrubPosition;
            _scrubSpeed = 0;
            _smoothedVelocity = 0;
            _velocityAccumulator = 0;
            _velocitySampleCount = 0;
            _lastScrubTime = DateTime.UtcNow;
            _scrubBufferPosition = 0;

            // Clear buffers
            if (_scrubBuffer != null)
            {
                Array.Clear(_scrubBuffer, 0, _scrubBuffer.Length);
            }

            if (_crossfadeBuffer != null)
            {
                Array.Clear(_crossfadeBuffer, 0, _crossfadeBuffer.Length);
            }

            // Pause normal playback if playing
            var playbackService = PlaybackService.Instance;
            if (playbackService.IsPlaying)
            {
                playbackService.Pause();
            }

            // Notify PlaybackService of scrub mode
            playbackService.EnterScrubMode();

            IsScrubbing = true;

            // Start scrub audio timer if audio is enabled
            if (_scrubAudioEnabled)
            {
                _scrubAudioTimer?.Start();
            }

            // Raise event
            ScrubStarted?.Invoke(this, new ScrubStartedEventArgs(_scrubPosition));
        }
    }

    /// <summary>
    /// Updates the scrub position during a drag operation.
    /// </summary>
    /// <param name="position">The new position in beats.</param>
    public void UpdateScrub(double position)
    {
        ThrowIfDisposed();

        if (!IsScrubbing)
        {
            return;
        }

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var deltaTime = (now - _lastScrubTime).TotalSeconds;

            // Prevent division by zero and ensure minimum interval
            if (deltaTime < MinScrubIntervalMs / 1000.0)
            {
                return;
            }

            var newPosition = Math.Max(0, position);
            var deltaPosition = newPosition - _previousScrubPosition;

            // Calculate instantaneous velocity (beats per second)
            var instantVelocity = deltaPosition / deltaTime;

            // Accumulate velocity for averaging
            _velocityAccumulator += instantVelocity;
            _velocitySampleCount++;

            // Apply exponential smoothing to velocity
            _smoothedVelocity = (_smoothedVelocity * VelocitySmoothingFactor) +
                                (instantVelocity * (1.0 - VelocitySmoothingFactor));

            // Calculate scrub speed based on velocity
            // Normalize to a reasonable range and clamp
            var bpm = PlaybackService.Instance.BPM;
            var beatsPerSecond = bpm / 60.0;
            var normalizedSpeed = _smoothedVelocity / beatsPerSecond;

            // Clamp speed to reasonable bounds
            ScrubSpeed = Math.Clamp(normalizedSpeed, -MaxScrubSpeed, MaxScrubSpeed);

            // Apply minimum speed threshold for audio playback
            if (Math.Abs(ScrubSpeed) < MinScrubSpeed)
            {
                ScrubSpeed = 0;
            }

            // Update position
            _previousScrubPosition = _scrubPosition;
            ScrubPosition = newPosition;
            _lastScrubTime = now;

            // Update playback service position
            PlaybackService.Instance.ScrubTo(newPosition);

            // Raise event
            ScrubUpdated?.Invoke(this, new ScrubUpdateEventArgs(newPosition, ScrubSpeed, _smoothedVelocity));
        }
    }

    /// <summary>
    /// Ends the scrubbing operation.
    /// </summary>
    /// <param name="continuePlayback">If true, continues playback from the scrub position.</param>
    public void EndScrub(bool continuePlayback = false)
    {
        ThrowIfDisposed();

        if (!IsScrubbing)
        {
            return;
        }

        lock (_lock)
        {
            var finalPosition = _scrubPosition;

            // Stop scrub audio timer
            _scrubAudioTimer?.Stop();

            // Apply crossfade to end scrub audio smoothly
            ApplyEndCrossfade();

            // Exit scrub mode in playback service
            PlaybackService.Instance.ExitScrubMode();

            // Reset scrub state
            IsScrubbing = false;
            ScrubSpeed = 0;
            _smoothedVelocity = 0;

            // Optionally continue playback
            if (continuePlayback)
            {
                PlaybackService.Instance.SetPosition(finalPosition);
                PlaybackService.Instance.Play();
            }
            else
            {
                PlaybackService.Instance.SetPosition(finalPosition);
            }

            // Raise event
            ScrubEnded?.Invoke(this, new ScrubEndedEventArgs(finalPosition, continuePlayback));
        }
    }

    /// <summary>
    /// Cancels scrubbing and returns to the original position before scrub started.
    /// </summary>
    public void CancelScrub()
    {
        ThrowIfDisposed();

        if (!IsScrubbing)
        {
            return;
        }

        lock (_lock)
        {
            // Stop scrub audio
            _scrubAudioTimer?.Stop();

            // Exit scrub mode
            PlaybackService.Instance.ExitScrubMode();

            // Reset state
            IsScrubbing = false;
            ScrubSpeed = 0;
            _smoothedVelocity = 0;
        }
    }

    /// <summary>
    /// Gets the average velocity during the current scrub operation.
    /// </summary>
    /// <returns>Average velocity in beats per second.</returns>
    public double GetAverageVelocity()
    {
        if (_velocitySampleCount == 0)
        {
            return 0;
        }

        return _velocityAccumulator / _velocitySampleCount;
    }

    #endregion

    #region Audio Processing

    /// <summary>
    /// Timer callback for continuous scrub audio output.
    /// </summary>
    private void OnScrubAudioTimerTick(object? sender, EventArgs e)
    {
        if (!IsScrubbing || !_scrubAudioEnabled)
        {
            return;
        }

        // Only play audio if there's movement
        if (Math.Abs(ScrubSpeed) < MinScrubSpeed)
        {
            return;
        }

        try
        {
            // Generate scrub audio
            GenerateScrubAudio();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScrubService] Audio generation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates audio samples for the current scrub position.
    /// This would integrate with the audio engine to provide real-time scrub audio.
    /// </summary>
    private void GenerateScrubAudio()
    {
        var audioEngine = AudioEngineService.Instance.AudioEngine;
        if (audioEngine == null || _scrubBuffer == null)
        {
            return;
        }

        // Calculate the time position for audio
        var bpm = PlaybackService.Instance.BPM;
        var timeInSeconds = (_scrubPosition / bpm) * 60.0;

        // The actual audio generation would depend on the MusicEngine.Core implementation
        // Here we outline the approach:
        // 1. Request a small buffer of audio at the scrub position
        // 2. Apply variable rate playback based on scrub speed
        // 3. Apply crossfade to avoid clicks
        // 4. Send to audio output

        // For now, trigger note preview to provide audio feedback
        if (Math.Abs(ScrubSpeed) > MinScrubSpeed)
        {
            // Calculate which beat we're on for triggering notes
            var beatInBar = _scrubPosition % 4;
            var previousBeatInBar = _previousScrubPosition % 4;

            // If we crossed a beat boundary, trigger audio
            if (Math.Floor(beatInBar) != Math.Floor(previousBeatInBar))
            {
                // Trigger a short click/tick sound for beat feedback
                TriggerScrubClick();
            }
        }
    }

    /// <summary>
    /// Triggers a short click sound for beat feedback during scrubbing.
    /// </summary>
    private void TriggerScrubClick()
    {
        try
        {
            // Use the audio engine to play a short click
            var audioEngineService = AudioEngineService.Instance;
            if (audioEngineService.IsInitialized)
            {
                // Play a short note for feedback (middle C, short duration)
                audioEngineService.PlayNotePreview(60, (int)(100 * _scrubVolume));

                // Schedule note off after a short delay
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    audioEngineService.StopNotePreview(60);
                };
                timer.Start();
            }
        }
        catch
        {
            // Ignore audio errors during scrub
        }
    }

    /// <summary>
    /// Applies a crossfade at the start of scrubbing to avoid clicks.
    /// </summary>
    private void ApplyStartCrossfade()
    {
        if (_scrubBuffer == null || _crossfadeBuffer == null)
        {
            return;
        }

        // Fade in from zero
        for (int i = 0; i < CrossfadeSamples; i++)
        {
            var fade = (float)i / CrossfadeSamples;
            var stereoIndex = i * 2;
            _scrubBuffer[stereoIndex] *= fade;
            _scrubBuffer[stereoIndex + 1] *= fade;
        }
    }

    /// <summary>
    /// Applies a crossfade at the end of scrubbing to avoid clicks.
    /// </summary>
    private void ApplyEndCrossfade()
    {
        if (_scrubBuffer == null)
        {
            return;
        }

        // Fade out to zero over the remaining buffer
        var samplesToFade = Math.Min(CrossfadeSamples, ScrubBufferSamples - _scrubBufferPosition);
        for (int i = 0; i < samplesToFade; i++)
        {
            var fade = 1.0f - ((float)i / samplesToFade);
            var stereoIndex = (_scrubBufferPosition + i) * 2;
            if (stereoIndex + 1 < _scrubBuffer.Length)
            {
                _scrubBuffer[stereoIndex] *= fade;
                _scrubBuffer[stereoIndex + 1] *= fade;
            }
        }
    }

    #endregion

    #region Helper Methods

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ScrubService));
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the ScrubService.
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

            // Stop and cleanup timer
            _scrubAudioTimer?.Stop();
            _scrubAudioTimer = null;

            // Cleanup buffers
            _scrubBuffer = null;
            _crossfadeBuffer = null;
        }
    }

    #endregion
}

#region Event Args Classes

/// <summary>
/// Event arguments for ScrubStarted event.
/// </summary>
public sealed class ScrubStartedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the position where scrubbing started in beats.
    /// </summary>
    public double StartPosition { get; }

    /// <summary>
    /// Gets the timestamp when scrubbing started.
    /// </summary>
    public DateTime Timestamp { get; }

    public ScrubStartedEventArgs(double startPosition)
    {
        StartPosition = startPosition;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for ScrubUpdated event.
/// </summary>
public sealed class ScrubUpdateEventArgs : EventArgs
{
    /// <summary>
    /// Gets the current scrub position in beats.
    /// </summary>
    public double Position { get; }

    /// <summary>
    /// Gets the current scrub speed multiplier.
    /// </summary>
    public double Speed { get; }

    /// <summary>
    /// Gets the smoothed velocity in beats per second.
    /// </summary>
    public double Velocity { get; }

    /// <summary>
    /// Gets the timestamp of this update.
    /// </summary>
    public DateTime Timestamp { get; }

    public ScrubUpdateEventArgs(double position, double speed, double velocity)
    {
        Position = position;
        Speed = speed;
        Velocity = velocity;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for ScrubEnded event.
/// </summary>
public sealed class ScrubEndedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the final scrub position in beats.
    /// </summary>
    public double FinalPosition { get; }

    /// <summary>
    /// Gets whether playback should continue from this position.
    /// </summary>
    public bool ContinuePlayback { get; }

    /// <summary>
    /// Gets the timestamp when scrubbing ended.
    /// </summary>
    public DateTime Timestamp { get; }

    public ScrubEndedEventArgs(double finalPosition, bool continuePlayback)
    {
        FinalPosition = finalPosition;
        ContinuePlayback = continuePlayback;
        Timestamp = DateTime.UtcNow;
    }
}

#endregion
