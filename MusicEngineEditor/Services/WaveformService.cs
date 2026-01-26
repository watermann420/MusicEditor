// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Waveform generation service.

using System.Collections.Concurrent;
using System.IO;
using MusicEngineEditor.Models;
using NAudio.Wave;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for generating and caching waveform data from audio files.
/// </summary>
public class WaveformService : IWaveformService, IDisposable
{
    private readonly ConcurrentDictionary<string, WaveformData> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<WaveformData>> _loadingTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();
    private bool _disposed;
    private long _currentCacheSize;

    /// <inheritdoc/>
    public int CacheCount => _cache.Count;

    /// <inheritdoc/>
    public long MaxCacheSize { get; set; } = 500 * 1024 * 1024; // 500MB default

    /// <inheritdoc/>
    public long CurrentCacheSize => _currentCacheSize;

    /// <inheritdoc/>
    public event EventHandler<WaveformLoadedEventArgs>? WaveformLoaded;

    /// <inheritdoc/>
    public event EventHandler<WaveformProgressEventArgs>? LoadProgress;

    /// <inheritdoc/>
    public async Task<WaveformData> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var normalizedPath = Path.GetFullPath(filePath);

        // Check cache first
        if (_cache.TryGetValue(normalizedPath, out var cached))
        {
            return cached;
        }

        // Check if already loading
        if (_loadingTasks.TryGetValue(normalizedPath, out var existingTask))
        {
            return await existingTask;
        }

        // Start new loading task
        var loadTask = LoadFromFileInternalAsync(normalizedPath, cancellationToken);
        _loadingTasks[normalizedPath] = loadTask;

        try
        {
            var result = await loadTask;
            return result;
        }
        finally
        {
            _loadingTasks.TryRemove(normalizedPath, out _);
        }
    }

    private async Task<WaveformData> LoadFromFileInternalAsync(string filePath, CancellationToken cancellationToken)
    {
        ReportProgress(filePath, 0, "Starting load...");

        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Audio file not found.", filePath);
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            WaveformData waveformData;

            ReportProgress(filePath, 10, "Reading audio file...");

            waveformData = await Task.Run(() => LoadAudioFile(filePath, extension, cancellationToken), cancellationToken);

            ReportProgress(filePath, 90, "Finalizing...");

            // Add to cache
            AddToCache(filePath, waveformData);

            ReportProgress(filePath, 100, "Complete");

            WaveformLoaded?.Invoke(this, new WaveformLoadedEventArgs(filePath, waveformData, true));

            return waveformData;
        }
        catch (OperationCanceledException)
        {
            var emptyData = new WaveformData();
            WaveformLoaded?.Invoke(this, new WaveformLoadedEventArgs(filePath, emptyData, false, "Loading cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading waveform from {filePath}: {ex.Message}");
            var emptyData = new WaveformData();
            WaveformLoaded?.Invoke(this, new WaveformLoadedEventArgs(filePath, emptyData, false, ex.Message));
            throw;
        }
    }

    private WaveformData LoadAudioFile(string filePath, string extension, CancellationToken cancellationToken)
    {
        using WaveStream reader = extension switch
        {
            ".mp3" => new Mp3FileReader(filePath),
            ".wav" => new WaveFileReader(filePath),
            ".aiff" or ".aif" => new AiffFileReader(filePath),
            _ => throw new NotSupportedException($"Audio format '{extension}' is not supported.")
        };

        var format = reader.WaveFormat;
        var sampleRate = format.SampleRate;
        var channelCount = format.Channels;
        var bytesPerSample = format.BitsPerSample / 8;
        var totalSamples = (int)(reader.Length / (bytesPerSample * channelCount));

        // Convert to float samples
        var samples = new float[totalSamples * channelCount];

        var sampleProvider = reader.ToSampleProvider();
        var buffer = new float[sampleRate * channelCount]; // 1 second buffer
        var offset = 0;
        int samplesRead;

        while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var copyLength = Math.Min(samplesRead, samples.Length - offset);
            if (copyLength <= 0) break;

            Array.Copy(buffer, 0, samples, offset, copyLength);
            offset += copyLength;

            var progress = 10 + (offset * 80.0 / samples.Length);
            ReportProgress(filePath, progress, $"Reading samples... {offset / (double)samples.Length:P0}");
        }

        // Trim array if needed
        if (offset < samples.Length)
        {
            Array.Resize(ref samples, offset);
        }

        return new WaveformData(samples, sampleRate, channelCount, filePath);
    }

    /// <inheritdoc/>
    public WaveformData GenerateFromSamples(float[] samples, int sampleRate, int channelCount)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (sampleRate <= 0) throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
        if (channelCount <= 0) throw new ArgumentException("Channel count must be positive.", nameof(channelCount));

        return new WaveformData(samples, sampleRate, channelCount);
    }

    /// <inheritdoc/>
    public WaveformPeak[] GeneratePeaks(float[] samples, int samplesPerPixel)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samplesPerPixel <= 0) throw new ArgumentException("Samples per pixel must be positive.", nameof(samplesPerPixel));

        if (samples.Length == 0)
            return [];

        var peakCount = (samples.Length + samplesPerPixel - 1) / samplesPerPixel;
        var peaks = new WaveformPeak[peakCount];

        for (var i = 0; i < peakCount; i++)
        {
            var startIndex = i * samplesPerPixel;
            var endIndex = Math.Min(startIndex + samplesPerPixel, samples.Length);

            var min = float.MaxValue;
            var max = float.MinValue;

            for (var j = startIndex; j < endIndex; j++)
            {
                var value = samples[j];
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            if (min == float.MaxValue)
            {
                min = 0f;
                max = 0f;
            }

            peaks[i] = new WaveformPeak(min, max);
        }

        return peaks;
    }

    /// <inheritdoc/>
    public WaveformData? GetCached(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return _cache.TryGetValue(normalizedPath, out var data) ? data : null;
    }

    /// <inheritdoc/>
    public bool IsCached(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return _cache.ContainsKey(normalizedPath);
    }

    /// <inheritdoc/>
    public void RemoveFromCache(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);

        if (_cache.TryRemove(normalizedPath, out var data))
        {
            var size = EstimateDataSize(data);
            Interlocked.Add(ref _currentCacheSize, -size);
        }
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
            _currentCacheSize = 0;
        }
    }

    private void AddToCache(string filePath, WaveformData data)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var size = EstimateDataSize(data);

        // Check if we need to evict items
        if (MaxCacheSize > 0)
        {
            while (_currentCacheSize + size > MaxCacheSize && _cache.Count > 0)
            {
                // Remove oldest item (simple FIFO eviction)
                foreach (var key in _cache.Keys)
                {
                    if (_cache.TryRemove(key, out var removed))
                    {
                        Interlocked.Add(ref _currentCacheSize, -EstimateDataSize(removed));
                        break;
                    }
                }
            }
        }

        if (_cache.TryAdd(normalizedPath, data))
        {
            Interlocked.Add(ref _currentCacheSize, size);
        }
    }

    private static long EstimateDataSize(WaveformData data)
    {
        // Estimate: 4 bytes per float sample + overhead
        return data.Samples.Length * sizeof(float) + 1024;
    }

    private void ReportProgress(string filePath, double progress, string message)
    {
        LoadProgress?.Invoke(this, new WaveformProgressEventArgs(filePath, progress, message));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            ClearCache();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
