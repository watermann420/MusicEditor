// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents a detected silence region in audio.
/// </summary>
public class SilenceRegion
{
    /// <summary>
    /// Start time of the silence in seconds.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// End time of the silence in seconds.
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// Duration of the silence in seconds.
    /// </summary>
    public double Duration => EndTime - StartTime;

    /// <summary>
    /// Average level during the silence (in dB).
    /// </summary>
    public double AverageLevelDb { get; set; }

    /// <summary>
    /// Peak level during the silence (in dB).
    /// </summary>
    public double PeakLevelDb { get; set; }
}

/// <summary>
/// Settings for silence detection.
/// </summary>
public class SilenceDetectionSettings
{
    /// <summary>
    /// Threshold in dB below which audio is considered silence.
    /// Default is -40 dB.
    /// </summary>
    public double ThresholdDb { get; set; } = -40.0;

    /// <summary>
    /// Minimum duration in seconds for a region to be considered silence.
    /// Default is 0.25 seconds.
    /// </summary>
    public double MinimumDurationSeconds { get; set; } = 0.25;

    /// <summary>
    /// Minimum gap between silence regions to merge them.
    /// If two silence regions are separated by less than this duration, they are merged.
    /// Default is 0.05 seconds.
    /// </summary>
    public double MergeGapSeconds { get; set; } = 0.05;

    /// <summary>
    /// Look-ahead time in seconds to avoid cutting into transients.
    /// Default is 0.01 seconds.
    /// </summary>
    public double LookAheadSeconds { get; set; } = 0.01;

    /// <summary>
    /// Whether to include the start/end of the audio if they are silent.
    /// Default is true.
    /// </summary>
    public bool IncludeStartEndSilence { get; set; } = true;
}

/// <summary>
/// Singleton service for detecting and analyzing silence in audio.
/// </summary>
public class SilenceDetectionService
{
    private static SilenceDetectionService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static SilenceDetectionService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SilenceDetectionService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Event raised when silence detection progress changes.
    /// </summary>
    public event EventHandler<double>? ProgressChanged;

    /// <summary>
    /// Event raised when silence regions are detected.
    /// </summary>
    public event EventHandler<List<SilenceRegion>>? SilenceDetected;

    private SilenceDetectionService()
    {
    }

    /// <summary>
    /// Detects silence regions in an audio buffer.
    /// </summary>
    /// <param name="samples">Audio samples (mono or interleaved stereo).</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <param name="channels">Number of channels (1 or 2).</param>
    /// <param name="settings">Detection settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of detected silence regions.</returns>
    public Task<List<SilenceRegion>> DetectSilenceAsync(
        float[] samples,
        int sampleRate,
        int channels = 1,
        SilenceDetectionSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            settings ??= new SilenceDetectionSettings();
            var regions = new List<SilenceRegion>();

            // Convert threshold from dB to linear
            double thresholdLinear = Math.Pow(10, settings.ThresholdDb / 20.0);

            // Analysis window size (approximately 10ms)
            int windowSize = Math.Max(1, sampleRate / 100);
            int totalWindows = samples.Length / (windowSize * channels);

            bool inSilence = false;
            double silenceStart = 0;
            double sumLevel = 0;
            double peakLevel = 0;
            int silenceWindowCount = 0;

            for (int window = 0; window < totalWindows; window++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Calculate RMS level for this window
                double sum = 0;
                double peak = 0;
                int startSample = window * windowSize * channels;
                int endSample = Math.Min(startSample + windowSize * channels, samples.Length);

                for (int i = startSample; i < endSample; i++)
                {
                    double sample = Math.Abs(samples[i]);
                    sum += sample * sample;
                    peak = Math.Max(peak, sample);
                }

                double rms = Math.Sqrt(sum / (endSample - startSample));
                double currentTime = (double)window * windowSize / sampleRate;

                if (rms < thresholdLinear && peak < thresholdLinear * 2)
                {
                    // We're in silence
                    if (!inSilence)
                    {
                        inSilence = true;
                        silenceStart = currentTime;
                        sumLevel = 0;
                        peakLevel = 0;
                        silenceWindowCount = 0;
                    }

                    sumLevel += rms;
                    peakLevel = Math.Max(peakLevel, peak);
                    silenceWindowCount++;
                }
                else
                {
                    // We're in audio
                    if (inSilence)
                    {
                        double silenceDuration = currentTime - silenceStart;

                        if (silenceDuration >= settings.MinimumDurationSeconds)
                        {
                            double avgLevel = silenceWindowCount > 0 ? sumLevel / silenceWindowCount : 0;
                            double avgDb = avgLevel > 0 ? 20 * Math.Log10(avgLevel) : -120;
                            double peakDb = peakLevel > 0 ? 20 * Math.Log10(peakLevel) : -120;

                            var region = new SilenceRegion
                            {
                                StartTime = silenceStart,
                                EndTime = currentTime,
                                AverageLevelDb = avgDb,
                                PeakLevelDb = peakDb
                            };

                            // Try to merge with previous region if gap is small
                            if (regions.Count > 0)
                            {
                                var lastRegion = regions[^1];
                                double gap = region.StartTime - lastRegion.EndTime;

                                if (gap < settings.MergeGapSeconds)
                                {
                                    // Merge regions
                                    lastRegion.EndTime = region.EndTime;
                                    lastRegion.AverageLevelDb = (lastRegion.AverageLevelDb + region.AverageLevelDb) / 2;
                                    lastRegion.PeakLevelDb = Math.Max(lastRegion.PeakLevelDb, region.PeakLevelDb);
                                }
                                else
                                {
                                    regions.Add(region);
                                }
                            }
                            else
                            {
                                regions.Add(region);
                            }
                        }

                        inSilence = false;
                    }
                }

                // Report progress
                if (window % 100 == 0)
                {
                    ProgressChanged?.Invoke(this, (double)window / totalWindows * 100);
                }
            }

            // Handle trailing silence
            if (inSilence && settings.IncludeStartEndSilence)
            {
                double totalDuration = (double)samples.Length / (sampleRate * channels);
                double silenceDuration = totalDuration - silenceStart;

                if (silenceDuration >= settings.MinimumDurationSeconds)
                {
                    double avgLevel = silenceWindowCount > 0 ? sumLevel / silenceWindowCount : 0;
                    double avgDb = avgLevel > 0 ? 20 * Math.Log10(avgLevel) : -120;
                    double peakDb = peakLevel > 0 ? 20 * Math.Log10(peakLevel) : -120;

                    regions.Add(new SilenceRegion
                    {
                        StartTime = silenceStart,
                        EndTime = totalDuration,
                        AverageLevelDb = avgDb,
                        PeakLevelDb = peakDb
                    });
                }
            }

            // Filter start silence if needed
            if (!settings.IncludeStartEndSilence && regions.Count > 0)
            {
                if (regions[0].StartTime < 0.001)
                {
                    regions.RemoveAt(0);
                }
            }

            ProgressChanged?.Invoke(this, 100);
            SilenceDetected?.Invoke(this, regions);

            return regions;
        }, cancellationToken);
    }

    /// <summary>
    /// Removes silence regions from audio samples.
    /// </summary>
    /// <param name="samples">Original audio samples.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="silenceRegions">Regions to remove.</param>
    /// <param name="keepPadding">Padding to keep at region boundaries in seconds.</param>
    /// <returns>Audio samples with silence removed.</returns>
    public float[] RemoveSilence(
        float[] samples,
        int sampleRate,
        int channels,
        List<SilenceRegion> silenceRegions,
        double keepPadding = 0.01)
    {
        if (silenceRegions.Count == 0)
        {
            return samples;
        }

        // Calculate total samples to keep
        var keepRanges = new List<(int Start, int End)>();
        int paddingSamples = (int)(keepPadding * sampleRate * channels);

        // Sort regions by start time
        var sortedRegions = silenceRegions.OrderBy(r => r.StartTime).ToList();

        int currentStart = 0;

        foreach (var region in sortedRegions)
        {
            int silenceStartSample = (int)(region.StartTime * sampleRate * channels);
            int silenceEndSample = (int)(region.EndTime * sampleRate * channels);

            // Add padding
            silenceStartSample = Math.Max(0, silenceStartSample - paddingSamples);
            silenceEndSample = Math.Min(samples.Length, silenceEndSample + paddingSamples);

            if (silenceStartSample > currentStart)
            {
                keepRanges.Add((currentStart, silenceStartSample));
            }

            currentStart = silenceEndSample;
        }

        // Add remaining audio after last silence
        if (currentStart < samples.Length)
        {
            keepRanges.Add((currentStart, samples.Length));
        }

        // Calculate total output size
        int totalOutputSamples = keepRanges.Sum(r => r.End - r.Start);
        var output = new float[totalOutputSamples];

        // Copy samples
        int outputIndex = 0;
        foreach (var (start, end) in keepRanges)
        {
            int length = end - start;
            Array.Copy(samples, start, output, outputIndex, length);
            outputIndex += length;
        }

        return output;
    }

    /// <summary>
    /// Splits audio at silence boundaries.
    /// </summary>
    /// <param name="samples">Audio samples.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="silenceRegions">Silence regions to split at.</param>
    /// <returns>List of audio segments with their start times.</returns>
    public List<(float[] Samples, double StartTime)> SplitAtSilence(
        float[] samples,
        int sampleRate,
        int channels,
        List<SilenceRegion> silenceRegions)
    {
        var segments = new List<(float[] Samples, double StartTime)>();

        if (silenceRegions.Count == 0)
        {
            segments.Add((samples, 0));
            return segments;
        }

        // Sort regions by start time
        var sortedRegions = silenceRegions.OrderBy(r => r.StartTime).ToList();

        int currentStart = 0;
        double currentStartTime = 0;

        foreach (var region in sortedRegions)
        {
            int silenceStartSample = (int)(region.StartTime * sampleRate * channels);

            if (silenceStartSample > currentStart)
            {
                int length = silenceStartSample - currentStart;
                var segment = new float[length];
                Array.Copy(samples, currentStart, segment, 0, length);
                segments.Add((segment, currentStartTime));
            }

            currentStart = (int)(region.EndTime * sampleRate * channels);
            currentStartTime = region.EndTime;
        }

        // Add final segment
        if (currentStart < samples.Length)
        {
            int length = samples.Length - currentStart;
            var segment = new float[length];
            Array.Copy(samples, currentStart, segment, 0, length);
            segments.Add((segment, currentStartTime));
        }

        return segments;
    }

    /// <summary>
    /// Gets statistics about silence in audio.
    /// </summary>
    /// <param name="samples">Audio samples.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="silenceRegions">Detected silence regions.</param>
    /// <returns>Statistics about silence.</returns>
    public SilenceStatistics GetStatistics(
        float[] samples,
        int sampleRate,
        int channels,
        List<SilenceRegion> silenceRegions)
    {
        double totalDuration = (double)samples.Length / (sampleRate * channels);
        double totalSilenceDuration = silenceRegions.Sum(r => r.Duration);

        return new SilenceStatistics
        {
            TotalDuration = totalDuration,
            TotalSilenceDuration = totalSilenceDuration,
            SilencePercentage = totalDuration > 0 ? (totalSilenceDuration / totalDuration) * 100 : 0,
            RegionCount = silenceRegions.Count,
            AverageSilenceDuration = silenceRegions.Count > 0 ? silenceRegions.Average(r => r.Duration) : 0,
            LongestSilenceDuration = silenceRegions.Count > 0 ? silenceRegions.Max(r => r.Duration) : 0,
            ShortestSilenceDuration = silenceRegions.Count > 0 ? silenceRegions.Min(r => r.Duration) : 0
        };
    }
}

/// <summary>
/// Statistics about silence in audio.
/// </summary>
public class SilenceStatistics
{
    /// <summary>Total audio duration in seconds.</summary>
    public double TotalDuration { get; set; }

    /// <summary>Total silence duration in seconds.</summary>
    public double TotalSilenceDuration { get; set; }

    /// <summary>Percentage of audio that is silence.</summary>
    public double SilencePercentage { get; set; }

    /// <summary>Number of silence regions.</summary>
    public int RegionCount { get; set; }

    /// <summary>Average duration of silence regions.</summary>
    public double AverageSilenceDuration { get; set; }

    /// <summary>Duration of the longest silence region.</summary>
    public double LongestSilenceDuration { get; set; }

    /// <summary>Duration of the shortest silence region.</summary>
    public double ShortestSilenceDuration { get; set; }
}
