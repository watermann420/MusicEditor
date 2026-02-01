using System;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngine.Core.Analysis;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service that processes audio data and provides reactive values for UI lighting effects.
/// Subscribes to AnalysisService and provides smoothed, interpolated values for glow effects.
/// </summary>
public sealed class AudioReactiveService : IDisposable
{
    private static readonly Lazy<AudioReactiveService> _instance = new(
        () => new AudioReactiveService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static AudioReactiveService Instance => _instance.Value;

    // Frequency band boundaries (Hz)
    private const float BassLowFreq = 20f;
    private const float BassMidFreq = 200f;
    private const float MidHighFreq = 2000f;
    private const float HighMaxFreq = 20000f;

    // Smoothing factors (0 = no smoothing, 1 = infinite smoothing)
    private const float AttackSmoothing = 0.3f;  // Fast attack
    private const float ReleaseSmoothing = 0.92f; // Slow release

    // Current reactive values (0.0 - 1.0)
    private float _bassLevel;
    private float _midLevel;
    private float _highLevel;
    private float _overallLevel;
    private float _peakLevel;

    // Smoothed values for UI
    private float _smoothedBass;
    private float _smoothedMid;
    private float _smoothedHigh;
    private float _smoothedOverall;
    private float _smoothedPeak;

    // Beat detection
    private float _beatIntensity;
    private float _lastBassLevel;
    private const float BeatThreshold = 0.15f;
    private const float BeatDecay = 0.85f;

    // Update timer
    private DispatcherTimer? _updateTimer;
    private bool _isRunning;
    private readonly object _lock = new();

    // Events for UI binding
    public event EventHandler<AudioReactiveEventArgs>? ValuesUpdated;

    /// <summary>
    /// Bass frequency level (20-200Hz), smoothed. Range: 0.0 - 1.0
    /// </summary>
    public float BassLevel => _smoothedBass;

    /// <summary>
    /// Mid frequency level (200-2000Hz), smoothed. Range: 0.0 - 1.0
    /// </summary>
    public float MidLevel => _smoothedMid;

    /// <summary>
    /// High frequency level (2000Hz+), smoothed. Range: 0.0 - 1.0
    /// </summary>
    public float HighLevel => _smoothedHigh;

    /// <summary>
    /// Overall RMS level, smoothed. Range: 0.0 - 1.0
    /// </summary>
    public float OverallLevel => _smoothedOverall;

    /// <summary>
    /// Peak level, smoothed. Range: 0.0 - 1.0
    /// </summary>
    public float PeakLevel => _smoothedPeak;

    /// <summary>
    /// Beat intensity for pulsing effects. Range: 0.0 - 1.0
    /// Spikes on bass transients, decays smoothly.
    /// </summary>
    public float BeatIntensity => _beatIntensity;

    /// <summary>
    /// Whether audio reactive updates are running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Sensitivity multiplier for all levels. Default: 1.5
    /// </summary>
    public float Sensitivity { get; set; } = 1.5f;

    /// <summary>
    /// Minimum glow opacity when audio is silent. Default: 0.1
    /// </summary>
    public float MinGlowOpacity { get; set; } = 0.1f;

    /// <summary>
    /// Maximum glow opacity at full volume. Default: 0.9
    /// </summary>
    public float MaxGlowOpacity { get; set; } = 0.9f;

    private AudioReactiveService()
    {
        // Private constructor for singleton
    }

    /// <summary>
    /// Starts the audio reactive service, subscribing to audio analysis events.
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;

        lock (_lock)
        {
            if (_isRunning) return;

            // Subscribe to AnalysisService events
            try
            {
                var analysisService = AnalysisService.Instance;
                analysisService.SpectrumUpdated += OnSpectrumUpdated;
                analysisService.PeakUpdated += OnPeakUpdated;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AudioReactiveService: Failed to subscribe to AnalysisService: {ex.Message}");
            }

            // Start UI update timer (60 fps for smooth animations)
            _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 fps
            };
            _updateTimer.Tick += OnUpdateTick;
            _updateTimer.Start();

            _isRunning = true;
        }
    }

    /// <summary>
    /// Stops the audio reactive service.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        lock (_lock)
        {
            if (!_isRunning) return;

            // Unsubscribe from events
            try
            {
                var analysisService = AnalysisService.Instance;
                analysisService.SpectrumUpdated -= OnSpectrumUpdated;
                analysisService.PeakUpdated -= OnPeakUpdated;
            }
            catch
            {
                // Ignore errors during cleanup
            }

            // Stop timer
            _updateTimer?.Stop();
            _updateTimer = null;

            _isRunning = false;

            // Reset values
            _smoothedBass = _smoothedMid = _smoothedHigh = _smoothedOverall = _smoothedPeak = 0f;
            _beatIntensity = 0f;
        }
    }

    private void OnSpectrumUpdated(object? sender, SpectrumEventArgs e)
    {
        if (e.Magnitudes == null || e.Frequencies == null || e.Magnitudes.Length == 0)
            return;

        // Calculate band levels from spectrum data
        float bassSum = 0f, midSum = 0f, highSum = 0f;
        int bassCount = 0, midCount = 0, highCount = 0;

        for (int i = 0; i < e.Magnitudes.Length && i < e.Frequencies.Length; i++)
        {
            float freq = e.Frequencies[i];
            float mag = e.Magnitudes[i];

            if (freq >= BassLowFreq && freq < BassMidFreq)
            {
                bassSum += mag;
                bassCount++;
            }
            else if (freq >= BassMidFreq && freq < MidHighFreq)
            {
                midSum += mag;
                midCount++;
            }
            else if (freq >= MidHighFreq && freq <= HighMaxFreq)
            {
                highSum += mag;
                highCount++;
            }
        }

        // Calculate averages (avoid division by zero)
        _bassLevel = bassCount > 0 ? (bassSum / bassCount) * Sensitivity : 0f;
        _midLevel = midCount > 0 ? (midSum / midCount) * Sensitivity : 0f;
        _highLevel = highCount > 0 ? (highSum / highCount) * Sensitivity : 0f;

        // Clamp to 0-1 range
        _bassLevel = Math.Clamp(_bassLevel, 0f, 1f);
        _midLevel = Math.Clamp(_midLevel, 0f, 1f);
        _highLevel = Math.Clamp(_highLevel, 0f, 1f);

        // Calculate overall from bands
        _overallLevel = (_bassLevel * 0.4f + _midLevel * 0.35f + _highLevel * 0.25f);

        // Beat detection based on bass transients
        float bassDelta = _bassLevel - _lastBassLevel;
        if (bassDelta > BeatThreshold)
        {
            _beatIntensity = Math.Min(1f, _beatIntensity + bassDelta * 2f);
        }
        _lastBassLevel = _bassLevel;
    }

    private void OnPeakUpdated(object? sender, PeakEventArgs e)
    {
        if (e.CurrentPeaks == null || e.CurrentPeaks.Length < 2)
            return;

        // Use max of left/right channels
        float peak = Math.Max(e.CurrentPeaks[0], e.CurrentPeaks[1]);
        _peakLevel = Math.Clamp(peak * Sensitivity, 0f, 1f);
    }

    private void OnUpdateTick(object? sender, EventArgs e)
    {
        // Smooth values using attack/release envelope
        _smoothedBass = SmoothValue(_smoothedBass, _bassLevel);
        _smoothedMid = SmoothValue(_smoothedMid, _midLevel);
        _smoothedHigh = SmoothValue(_smoothedHigh, _highLevel);
        _smoothedOverall = SmoothValue(_smoothedOverall, _overallLevel);
        _smoothedPeak = SmoothValue(_smoothedPeak, _peakLevel);

        // Decay beat intensity
        _beatIntensity *= BeatDecay;
        if (_beatIntensity < 0.01f) _beatIntensity = 0f;

        // Raise event for UI updates
        ValuesUpdated?.Invoke(this, new AudioReactiveEventArgs
        {
            Bass = _smoothedBass,
            Mid = _smoothedMid,
            High = _smoothedHigh,
            Overall = _smoothedOverall,
            Peak = _smoothedPeak,
            Beat = _beatIntensity
        });
    }

    private static float SmoothValue(float current, float target)
    {
        // Use different smoothing for attack vs release
        float smoothing = target > current ? AttackSmoothing : ReleaseSmoothing;
        return current + (target - current) * (1f - smoothing);
    }

    /// <summary>
    /// Converts a reactive level to glow opacity within configured range.
    /// </summary>
    public float LevelToGlowOpacity(float level)
    {
        return MinGlowOpacity + (MaxGlowOpacity - MinGlowOpacity) * level;
    }

    /// <summary>
    /// Converts a reactive level to glow blur radius.
    /// </summary>
    public float LevelToBlurRadius(float level, float minRadius = 4f, float maxRadius = 20f)
    {
        return minRadius + (maxRadius - minRadius) * level;
    }

    /// <summary>
    /// Interpolates between two colors based on level.
    /// </summary>
    public Color LevelToColor(float level, Color lowColor, Color highColor)
    {
        byte r = (byte)(lowColor.R + (highColor.R - lowColor.R) * level);
        byte g = (byte)(lowColor.G + (highColor.G - lowColor.G) * level);
        byte b = (byte)(lowColor.B + (highColor.B - lowColor.B) * level);
        return Color.FromRgb(r, g, b);
    }

    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// Event arguments for audio reactive value updates.
/// </summary>
public class AudioReactiveEventArgs : EventArgs
{
    /// <summary>Bass frequency level (0.0 - 1.0)</summary>
    public float Bass { get; init; }

    /// <summary>Mid frequency level (0.0 - 1.0)</summary>
    public float Mid { get; init; }

    /// <summary>High frequency level (0.0 - 1.0)</summary>
    public float High { get; init; }

    /// <summary>Overall level (0.0 - 1.0)</summary>
    public float Overall { get; init; }

    /// <summary>Peak level (0.0 - 1.0)</summary>
    public float Peak { get; init; }

    /// <summary>Beat intensity for pulsing (0.0 - 1.0)</summary>
    public float Beat { get; init; }
}
