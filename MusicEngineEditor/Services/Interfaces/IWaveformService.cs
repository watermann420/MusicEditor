// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Waveform generation service.

using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for generating and managing waveform data for audio visualization.
/// </summary>
public interface IWaveformService
{
    /// <summary>
    /// Loads waveform data from an audio file.
    /// </summary>
    /// <param name="filePath">Path to the audio file (WAV, MP3, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Loaded waveform data.</returns>
    Task<WaveformData> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates waveform data from raw audio samples.
    /// </summary>
    /// <param name="samples">Raw audio samples (interleaved for stereo).</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channelCount">Number of channels.</param>
    /// <returns>Waveform data with generated peaks.</returns>
    WaveformData GenerateFromSamples(float[] samples, int sampleRate, int channelCount);

    /// <summary>
    /// Generates peak data for the specified samples at a given zoom level.
    /// </summary>
    /// <param name="samples">Raw audio samples (single channel).</param>
    /// <param name="samplesPerPixel">Number of samples to aggregate per pixel.</param>
    /// <returns>Array of peak data.</returns>
    WaveformPeak[] GeneratePeaks(float[] samples, int samplesPerPixel);

    /// <summary>
    /// Gets cached waveform data for a file path if available.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <returns>Cached waveform data, or null if not cached.</returns>
    WaveformData? GetCached(string filePath);

    /// <summary>
    /// Checks if waveform data is cached for the specified file.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <returns>True if cached, false otherwise.</returns>
    bool IsCached(string filePath);

    /// <summary>
    /// Removes waveform data from the cache.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    void RemoveFromCache(string filePath);

    /// <summary>
    /// Clears all cached waveform data.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets the number of items currently in the cache.
    /// </summary>
    int CacheCount { get; }

    /// <summary>
    /// Gets the maximum cache size in bytes (0 for unlimited).
    /// </summary>
    long MaxCacheSize { get; set; }

    /// <summary>
    /// Gets the current cache size in bytes.
    /// </summary>
    long CurrentCacheSize { get; }

    /// <summary>
    /// Event raised when a waveform is loaded from a file.
    /// </summary>
    event EventHandler<WaveformLoadedEventArgs>? WaveformLoaded;

    /// <summary>
    /// Event raised when loading progress updates.
    /// </summary>
    event EventHandler<WaveformProgressEventArgs>? LoadProgress;
}

/// <summary>
/// Event arguments for waveform loaded event.
/// </summary>
public class WaveformLoadedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the file path that was loaded.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the loaded waveform data.
    /// </summary>
    public WaveformData WaveformData { get; }

    /// <summary>
    /// Gets whether the load was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the error message if loading failed.
    /// </summary>
    public string? ErrorMessage { get; }

    public WaveformLoadedEventArgs(string filePath, WaveformData waveformData, bool success, string? errorMessage = null)
    {
        FilePath = filePath;
        WaveformData = waveformData;
        Success = success;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Event arguments for waveform loading progress.
/// </summary>
public class WaveformProgressEventArgs : EventArgs
{
    /// <summary>
    /// Gets the file path being loaded.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the progress percentage (0-100).
    /// </summary>
    public double Progress { get; }

    /// <summary>
    /// Gets the current status message.
    /// </summary>
    public string StatusMessage { get; }

    public WaveformProgressEventArgs(string filePath, double progress, string statusMessage)
    {
        FilePath = filePath;
        Progress = progress;
        StatusMessage = statusMessage;
    }
}
