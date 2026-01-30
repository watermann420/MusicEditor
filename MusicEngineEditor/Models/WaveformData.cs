// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Waveform visualization data model.

namespace MusicEngineEditor.Models;

/// <summary>
/// Color modes for waveform visualization.
/// </summary>
public enum WaveformColorMode
{
    /// <summary>
    /// No coloring, use default waveform color.
    /// </summary>
    Off,

    /// <summary>
    /// Color by frequency content (bass=red, mids=green, highs=blue).
    /// </summary>
    Frequency,

    /// <summary>
    /// Color by loudness (gradient from quiet to loud).
    /// </summary>
    Loudness
}

/// <summary>
/// Represents frequency band energy levels for waveform coloring.
/// </summary>
public readonly struct FrequencyBands
{
    /// <summary>
    /// Gets the bass frequency energy (0-300Hz), normalized 0-1.
    /// </summary>
    public float Bass { get; init; }

    /// <summary>
    /// Gets the mid frequency energy (300-2000Hz), normalized 0-1.
    /// </summary>
    public float Mids { get; init; }

    /// <summary>
    /// Gets the high frequency energy (2000Hz+), normalized 0-1.
    /// </summary>
    public float Highs { get; init; }

    public FrequencyBands(float bass, float mids, float highs)
    {
        Bass = bass;
        Mids = mids;
        Highs = highs;
    }

    /// <summary>
    /// Returns an empty frequency bands structure.
    /// </summary>
    public static FrequencyBands Empty => new(0f, 0f, 0f);

    /// <summary>
    /// Combines two frequency bands by averaging.
    /// </summary>
    public static FrequencyBands Average(FrequencyBands a, FrequencyBands b) =>
        new((a.Bass + b.Bass) / 2f, (a.Mids + b.Mids) / 2f, (a.Highs + b.Highs) / 2f);
}

/// <summary>
/// Represents a single peak data point for waveform visualization.
/// Contains the minimum and maximum sample values for a range of samples.
/// </summary>
public readonly struct WaveformPeak
{
    /// <summary>
    /// Gets the minimum sample value in this peak range.
    /// </summary>
    public float Min { get; init; }

    /// <summary>
    /// Gets the maximum sample value in this peak range.
    /// </summary>
    public float Max { get; init; }

    /// <summary>
    /// Gets the RMS (loudness) value for this peak range, normalized 0-1.
    /// </summary>
    public float Rms { get; init; }

    /// <summary>
    /// Gets the frequency band energies for this peak range.
    /// </summary>
    public FrequencyBands FrequencyBands { get; init; }

    public WaveformPeak(float min, float max)
    {
        Min = min;
        Max = max;
        Rms = 0f;
        FrequencyBands = FrequencyBands.Empty;
    }

    public WaveformPeak(float min, float max, float rms, FrequencyBands frequencyBands)
    {
        Min = min;
        Max = max;
        Rms = rms;
        FrequencyBands = frequencyBands;
    }

    /// <summary>
    /// Creates a peak from a single sample value.
    /// </summary>
    public static WaveformPeak FromSample(float sample) => new(sample, sample);

    /// <summary>
    /// Combines two peaks into one covering both ranges.
    /// </summary>
    public static WaveformPeak Combine(WaveformPeak a, WaveformPeak b) =>
        new(Math.Min(a.Min, b.Min), Math.Max(a.Max, b.Max),
            (a.Rms + b.Rms) / 2f,
            FrequencyBands.Average(a.FrequencyBands, b.FrequencyBands));
}

/// <summary>
/// Stores waveform peak data for visualization with caching for different zoom levels.
/// </summary>
public class WaveformData
{
    private readonly Dictionary<int, WaveformPeak[][]> _peakCache = [];
    private readonly object _cacheLock = new();

    /// <summary>
    /// Gets the raw audio samples (interleaved if stereo).
    /// </summary>
    public float[] Samples { get; private set; } = [];

    /// <summary>
    /// Gets the sample rate in Hz.
    /// </summary>
    public int SampleRate { get; private set; }

    /// <summary>
    /// Gets the number of audio channels.
    /// </summary>
    public int ChannelCount { get; private set; }

    /// <summary>
    /// Gets the duration in seconds.
    /// </summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>
    /// Gets the total number of samples per channel.
    /// </summary>
    public int SamplesPerChannel => ChannelCount > 0 ? Samples.Length / ChannelCount : 0;

    /// <summary>
    /// Gets the source file path, if loaded from a file.
    /// </summary>
    public string? SourceFilePath { get; private set; }

    /// <summary>
    /// Gets whether the waveform data is loaded and valid.
    /// </summary>
    public bool IsLoaded => Samples.Length > 0 && SampleRate > 0 && ChannelCount > 0;

    /// <summary>
    /// Initializes a new empty instance of WaveformData.
    /// </summary>
    public WaveformData()
    {
    }

    /// <summary>
    /// Initializes a new instance of WaveformData from raw samples.
    /// </summary>
    /// <param name="samples">Raw audio samples (interleaved for stereo).</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channelCount">Number of audio channels.</param>
    /// <param name="sourceFilePath">Optional source file path.</param>
    public WaveformData(float[] samples, int sampleRate, int channelCount, string? sourceFilePath = null)
    {
        SetData(samples, sampleRate, channelCount, sourceFilePath);
    }

    /// <summary>
    /// Sets the waveform data from raw samples.
    /// </summary>
    public void SetData(float[] samples, int sampleRate, int channelCount, string? sourceFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (sampleRate <= 0) throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
        if (channelCount <= 0) throw new ArgumentException("Channel count must be positive.", nameof(channelCount));

        Samples = samples;
        SampleRate = sampleRate;
        ChannelCount = channelCount;
        SourceFilePath = sourceFilePath;

        var samplesPerChannel = samples.Length / channelCount;
        Duration = TimeSpan.FromSeconds((double)samplesPerChannel / sampleRate);

        // Clear cache when data changes
        ClearCache();
    }

    /// <summary>
    /// Clears the peak data cache.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _peakCache.Clear();
        }
    }

    /// <summary>
    /// Gets peak data for the specified zoom level (samples per pixel).
    /// </summary>
    /// <param name="samplesPerPixel">Number of samples to aggregate into each peak.</param>
    /// <returns>Array of peak data per channel.</returns>
    public WaveformPeak[][] GetPeaks(int samplesPerPixel)
    {
        if (!IsLoaded || samplesPerPixel <= 0)
            return [];

        // Normalize to nearest power of 2 for better caching
        var normalizedSpp = NormalizeSamplesPerPixel(samplesPerPixel);

        lock (_cacheLock)
        {
            if (_peakCache.TryGetValue(normalizedSpp, out var cached))
                return cached;
        }

        var peaks = GeneratePeaks(normalizedSpp);

        lock (_cacheLock)
        {
            _peakCache[normalizedSpp] = peaks;
        }

        return peaks;
    }

    /// <summary>
    /// Gets peak data for a specific channel.
    /// </summary>
    /// <param name="channel">Channel index (0-based).</param>
    /// <param name="samplesPerPixel">Number of samples per pixel.</param>
    /// <returns>Peak data array for the specified channel.</returns>
    public WaveformPeak[] GetChannelPeaks(int channel, int samplesPerPixel)
    {
        if (channel < 0 || channel >= ChannelCount)
            return [];

        var allPeaks = GetPeaks(samplesPerPixel);
        return channel < allPeaks.Length ? allPeaks[channel] : [];
    }

    /// <summary>
    /// Gets mixed (mono) peak data combining all channels.
    /// </summary>
    /// <param name="samplesPerPixel">Number of samples per pixel.</param>
    /// <returns>Mixed peak data array.</returns>
    public WaveformPeak[] GetMixedPeaks(int samplesPerPixel)
    {
        var allPeaks = GetPeaks(samplesPerPixel);
        if (allPeaks.Length == 0)
            return [];

        if (allPeaks.Length == 1)
            return allPeaks[0];

        // Mix all channels
        var length = allPeaks[0].Length;
        var mixed = new WaveformPeak[length];

        for (var i = 0; i < length; i++)
        {
            var min = float.MaxValue;
            var max = float.MinValue;

            foreach (var channelPeaks in allPeaks)
            {
                if (i < channelPeaks.Length)
                {
                    min = Math.Min(min, channelPeaks[i].Min);
                    max = Math.Max(max, channelPeaks[i].Max);
                }
            }

            mixed[i] = new WaveformPeak(min, max);
        }

        return mixed;
    }

    /// <summary>
    /// Gets peak data for a specific sample range.
    /// </summary>
    /// <param name="startSample">Start sample index.</param>
    /// <param name="endSample">End sample index.</param>
    /// <param name="targetWidth">Target width in pixels.</param>
    /// <returns>Peak data array for all channels.</returns>
    public WaveformPeak[][] GetPeaksForRange(int startSample, int endSample, int targetWidth)
    {
        if (!IsLoaded || targetWidth <= 0)
            return [];

        startSample = Math.Max(0, startSample);
        endSample = Math.Min(SamplesPerChannel, endSample);

        if (startSample >= endSample)
            return [];

        var rangeLength = endSample - startSample;
        var samplesPerPixel = Math.Max(1, rangeLength / targetWidth);

        var result = new WaveformPeak[ChannelCount][];

        for (var channel = 0; channel < ChannelCount; channel++)
        {
            var peaks = new List<WaveformPeak>();
            var sampleIndex = startSample;

            while (sampleIndex < endSample)
            {
                var chunkEnd = Math.Min(sampleIndex + samplesPerPixel, endSample);
                var min = float.MaxValue;
                var max = float.MinValue;

                for (var i = sampleIndex; i < chunkEnd; i++)
                {
                    var value = GetSample(channel, i);
                    min = Math.Min(min, value);
                    max = Math.Max(max, value);
                }

                peaks.Add(new WaveformPeak(min, max));
                sampleIndex = chunkEnd;
            }

            result[channel] = peaks.ToArray();
        }

        return result;
    }

    /// <summary>
    /// Gets a single sample value for a specific channel and index.
    /// </summary>
    /// <param name="channel">Channel index.</param>
    /// <param name="sampleIndex">Sample index within the channel.</param>
    /// <returns>Sample value.</returns>
    public float GetSample(int channel, int sampleIndex)
    {
        if (channel < 0 || channel >= ChannelCount)
            return 0f;

        var index = sampleIndex * ChannelCount + channel;
        if (index < 0 || index >= Samples.Length)
            return 0f;

        return Samples[index];
    }

    /// <summary>
    /// Generates peak data for all channels at the specified samples per pixel.
    /// </summary>
    private WaveformPeak[][] GeneratePeaks(int samplesPerPixel)
    {
        var samplesPerChannel = SamplesPerChannel;
        var peakCount = (samplesPerChannel + samplesPerPixel - 1) / samplesPerPixel;

        var result = new WaveformPeak[ChannelCount][];

        for (var channel = 0; channel < ChannelCount; channel++)
        {
            var channelPeaks = new WaveformPeak[peakCount];

            for (var i = 0; i < peakCount; i++)
            {
                var startSample = i * samplesPerPixel;
                var endSample = Math.Min(startSample + samplesPerPixel, samplesPerChannel);

                var min = float.MaxValue;
                var max = float.MinValue;
                var sumSquares = 0.0;
                var sampleCount = endSample - startSample;

                // For frequency analysis, collect samples
                var chunkSamples = new float[sampleCount];
                var sampleIdx = 0;

                for (var j = startSample; j < endSample; j++)
                {
                    var value = GetSample(channel, j);
                    min = Math.Min(min, value);
                    max = Math.Max(max, value);
                    sumSquares += value * value;
                    chunkSamples[sampleIdx++] = value;
                }

                if (min == float.MaxValue)
                {
                    min = 0f;
                    max = 0f;
                }

                // Calculate RMS (normalized to 0-1 range, with typical audio being around 0.1-0.3)
                var rms = sampleCount > 0 ? (float)Math.Sqrt(sumSquares / sampleCount) : 0f;

                // Calculate frequency bands using zero-crossing rate approximation
                // This is a computationally efficient alternative to FFT for visualization
                var frequencyBands = AnalyzeFrequencyBands(chunkSamples, SampleRate);

                channelPeaks[i] = new WaveformPeak(min, max, rms, frequencyBands);
            }

            result[channel] = channelPeaks;
        }

        return result;
    }

    /// <summary>
    /// Analyzes frequency content using a simplified band-pass filter approach.
    /// This is more efficient than FFT for visualization purposes.
    /// </summary>
    private static FrequencyBands AnalyzeFrequencyBands(float[] samples, int sampleRate)
    {
        if (samples.Length < 4)
            return FrequencyBands.Empty;

        // Simple IIR filter coefficients for three frequency bands
        // Bass: 20-300Hz, Mids: 300-2000Hz, Highs: 2000Hz+

        float bassEnergy = 0f, midsEnergy = 0f, highsEnergy = 0f;

        // Low-pass filter state for bass (cutoff ~300Hz)
        float bassLp = 0f;
        var bassAlpha = Math.Min(0.99f, 2.0f * MathF.PI * 300f / sampleRate);

        // Band-pass approximation using difference of low-pass filters
        float midLp1 = 0f, midLp2 = 0f;
        var midAlpha1 = Math.Min(0.99f, 2.0f * MathF.PI * 2000f / sampleRate);
        var midAlpha2 = Math.Min(0.99f, 2.0f * MathF.PI * 300f / sampleRate);

        // High-pass filter state for highs (cutoff ~2000Hz)
        float highHp = 0f;
        var highAlpha = Math.Min(0.99f, 2.0f * MathF.PI * 2000f / sampleRate);

        for (var i = 0; i < samples.Length; i++)
        {
            var sample = samples[i];

            // Bass: low-pass filter
            bassLp += bassAlpha * (sample - bassLp);
            var bassValue = bassLp;
            bassEnergy += bassValue * bassValue;

            // Mids: band-pass (difference of two low-pass filters)
            midLp1 += midAlpha1 * (sample - midLp1);
            midLp2 += midAlpha2 * (sample - midLp2);
            var midsValue = midLp1 - midLp2;
            midsEnergy += midsValue * midsValue;

            // Highs: high-pass filter
            var prevHighHp = highHp;
            highHp = (1f - highAlpha) * (prevHighHp + sample - (i > 0 ? samples[i - 1] : 0f));
            var highsValue = highHp;
            highsEnergy += highsValue * highsValue;
        }

        // Normalize energies
        var totalEnergy = bassEnergy + midsEnergy + highsEnergy;
        if (totalEnergy < 0.0001f)
            return FrequencyBands.Empty;

        // Convert to RMS and normalize relative to each other
        var bassRms = MathF.Sqrt(bassEnergy / samples.Length);
        var midsRms = MathF.Sqrt(midsEnergy / samples.Length);
        var highsRms = MathF.Sqrt(highsEnergy / samples.Length);

        // Normalize to 0-1 range relative to maximum
        var maxRms = Math.Max(bassRms, Math.Max(midsRms, highsRms));
        if (maxRms < 0.0001f)
            return FrequencyBands.Empty;

        return new FrequencyBands(
            Math.Min(1f, bassRms / maxRms),
            Math.Min(1f, midsRms / maxRms),
            Math.Min(1f, highsRms / maxRms)
        );
    }

    /// <summary>
    /// Normalizes samples per pixel to the nearest power of 2 for caching efficiency.
    /// </summary>
    private static int NormalizeSamplesPerPixel(int samplesPerPixel)
    {
        if (samplesPerPixel <= 0) return 1;
        if (samplesPerPixel <= 1) return 1;

        // Find nearest power of 2
        var power = (int)Math.Ceiling(Math.Log2(samplesPerPixel));
        return 1 << power;
    }

    /// <summary>
    /// Converts a time position to a sample index.
    /// </summary>
    /// <param name="time">Time position.</param>
    /// <returns>Sample index.</returns>
    public int TimeToSample(TimeSpan time)
    {
        return (int)(time.TotalSeconds * SampleRate);
    }

    /// <summary>
    /// Converts a sample index to a time position.
    /// </summary>
    /// <param name="sampleIndex">Sample index.</param>
    /// <returns>Time position.</returns>
    public TimeSpan SampleToTime(int sampleIndex)
    {
        return TimeSpan.FromSeconds((double)sampleIndex / SampleRate);
    }

    /// <summary>
    /// Creates a deep copy of this waveform data.
    /// </summary>
    public WaveformData Clone()
    {
        var clone = new WaveformData();
        var samplesCopy = new float[Samples.Length];
        Array.Copy(Samples, samplesCopy, Samples.Length);
        clone.SetData(samplesCopy, SampleRate, ChannelCount, SourceFilePath);
        return clone;
    }
}
