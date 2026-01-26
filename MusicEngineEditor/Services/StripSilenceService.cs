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
/// Represents a non-silent audio region (clip) detected by strip silence.
/// </summary>
public class StripSilenceClip
{
    /// <summary>
    /// Unique identifier for the clip.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Start time of the clip in seconds.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// End time of the clip in seconds.
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// Duration of the clip in seconds.
    /// </summary>
    public double Duration => EndTime - StartTime;

    /// <summary>
    /// Peak level of the clip in dB.
    /// </summary>
    public double PeakLevelDb { get; set; }

    /// <summary>
    /// RMS level of the clip in dB.
    /// </summary>
    public double RmsLevelDb { get; set; }

    /// <summary>
    /// Whether this clip should be included in the result.
    /// </summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>
    /// Audio samples for this clip (optional, populated on export).
    /// </summary>
    public float[]? Samples { get; set; }
}

/// <summary>
/// Settings for strip silence processing.
/// </summary>
public class StripSilenceSettings
{
    /// <summary>
    /// Threshold in dB below which audio is considered silence.
    /// Default is -40 dB.
    /// </summary>
    public double ThresholdDb { get; set; } = -40.0;

    /// <summary>
    /// Attack time in milliseconds - how quickly the gate opens.
    /// Default is 5 ms.
    /// </summary>
    public double AttackMs { get; set; } = 5.0;

    /// <summary>
    /// Release time in milliseconds - how quickly the gate closes.
    /// Default is 50 ms.
    /// </summary>
    public double ReleaseMs { get; set; } = 50.0;

    /// <summary>
    /// Hold time in milliseconds - minimum time to stay open after threshold is exceeded.
    /// Default is 100 ms.
    /// </summary>
    public double HoldMs { get; set; } = 100.0;

    /// <summary>
    /// Minimum clip length in milliseconds.
    /// Clips shorter than this are discarded.
    /// Default is 100 ms.
    /// </summary>
    public double MinimumClipLengthMs { get; set; } = 100.0;

    /// <summary>
    /// Pre-roll in milliseconds - extra audio to include before each clip starts.
    /// Default is 10 ms.
    /// </summary>
    public double PreRollMs { get; set; } = 10.0;

    /// <summary>
    /// Post-roll in milliseconds - extra audio to include after each clip ends.
    /// Default is 10 ms.
    /// </summary>
    public double PostRollMs { get; set; } = 10.0;

    /// <summary>
    /// Whether to create fade-in at the start of each clip.
    /// Default is true.
    /// </summary>
    public bool ApplyFadeIn { get; set; } = true;

    /// <summary>
    /// Whether to create fade-out at the end of each clip.
    /// Default is true.
    /// </summary>
    public bool ApplyFadeOut { get; set; } = true;

    /// <summary>
    /// Fade length in milliseconds.
    /// Default is 5 ms.
    /// </summary>
    public double FadeLengthMs { get; set; } = 5.0;

    /// <summary>
    /// Hysteresis in dB - difference between open and close threshold.
    /// Default is 3 dB.
    /// </summary>
    public double HysteresisDb { get; set; } = 3.0;

    /// <summary>
    /// Look-ahead in milliseconds for detecting transients.
    /// Default is 5 ms.
    /// </summary>
    public double LookAheadMs { get; set; } = 5.0;
}

/// <summary>
/// Result of strip silence analysis.
/// </summary>
public class StripSilenceResult
{
    /// <summary>
    /// Detected non-silent clips.
    /// </summary>
    public List<StripSilenceClip> Clips { get; set; } = [];

    /// <summary>
    /// Total audio duration in seconds.
    /// </summary>
    public double TotalDuration { get; set; }

    /// <summary>
    /// Total duration of non-silent audio in seconds.
    /// </summary>
    public double NonSilentDuration { get; set; }

    /// <summary>
    /// Percentage of audio that is non-silent.
    /// </summary>
    public double NonSilentPercentage => TotalDuration > 0 ? (NonSilentDuration / TotalDuration) * 100 : 0;

    /// <summary>
    /// Settings used for analysis.
    /// </summary>
    public StripSilenceSettings Settings { get; set; } = new();
}

/// <summary>
/// Singleton service for strip silence (gate-style clip splitting).
/// Creates individual clips from non-silent regions of audio.
/// </summary>
public class StripSilenceService
{
    private static StripSilenceService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static StripSilenceService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new StripSilenceService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Event raised when analysis progress changes.
    /// </summary>
    public event EventHandler<double>? ProgressChanged;

    /// <summary>
    /// Event raised when analysis is complete.
    /// </summary>
    public event EventHandler<StripSilenceResult>? AnalysisComplete;

    private StripSilenceService()
    {
    }

    /// <summary>
    /// Analyzes audio and detects non-silent regions using gate-style processing.
    /// </summary>
    /// <param name="samples">Audio samples.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="settings">Strip silence settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis result with detected clips.</returns>
    public Task<StripSilenceResult> AnalyzeAsync(
        float[] samples,
        int sampleRate,
        int channels = 1,
        StripSilenceSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            settings ??= new StripSilenceSettings();
            var result = new StripSilenceResult { Settings = settings };

            // Convert settings to samples
            double thresholdLinear = Math.Pow(10, settings.ThresholdDb / 20.0);
            double closeThresholdLinear = Math.Pow(10, (settings.ThresholdDb - settings.HysteresisDb) / 20.0);
            int attackSamples = (int)(settings.AttackMs * sampleRate / 1000.0);
            int releaseSamples = (int)(settings.ReleaseMs * sampleRate / 1000.0);
            int holdSamples = (int)(settings.HoldMs * sampleRate / 1000.0);
            int minClipSamples = (int)(settings.MinimumClipLengthMs * sampleRate / 1000.0) * channels;
            int preRollSamples = (int)(settings.PreRollMs * sampleRate / 1000.0) * channels;
            int postRollSamples = (int)(settings.PostRollMs * sampleRate / 1000.0) * channels;
            int lookAheadSamples = (int)(settings.LookAheadMs * sampleRate / 1000.0) * channels;

            // Gate state
            bool gateOpen = false;
            int holdCounter = 0;
            int attackCounter = 0;
            int releaseCounter = 0;

            // Current clip tracking
            int clipStartSample = 0;
            double clipPeak = 0;
            double clipRmsSum = 0;
            int clipSampleCount = 0;

            var clips = new List<StripSilenceClip>();
            int totalSamples = samples.Length;

            // Process with look-ahead
            for (int i = 0; i < totalSamples; i += channels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Calculate current level (max of channels)
                double level = 0;
                for (int ch = 0; ch < channels && (i + ch) < totalSamples; ch++)
                {
                    level = Math.Max(level, Math.Abs(samples[i + ch]));
                }

                // Look ahead for transients
                double lookAheadLevel = level;
                for (int la = 0; la < lookAheadSamples && (i + la * channels) < totalSamples; la += channels)
                {
                    for (int ch = 0; ch < channels; ch++)
                    {
                        int idx = i + la + ch;
                        if (idx < totalSamples)
                        {
                            lookAheadLevel = Math.Max(lookAheadLevel, Math.Abs(samples[idx]));
                        }
                    }
                }

                bool shouldOpen = lookAheadLevel >= thresholdLinear;
                bool shouldClose = level < closeThresholdLinear;

                // Gate state machine
                if (!gateOpen)
                {
                    if (shouldOpen)
                    {
                        attackCounter++;
                        if (attackCounter >= attackSamples)
                        {
                            gateOpen = true;
                            holdCounter = holdSamples;
                            attackCounter = 0;
                            releaseCounter = 0;

                            // Start new clip with pre-roll
                            clipStartSample = Math.Max(0, i - preRollSamples);
                            clipPeak = level;
                            clipRmsSum = level * level;
                            clipSampleCount = 1;
                        }
                    }
                    else
                    {
                        attackCounter = 0;
                    }
                }
                else
                {
                    // Gate is open - track levels
                    clipPeak = Math.Max(clipPeak, level);
                    clipRmsSum += level * level;
                    clipSampleCount++;

                    if (shouldClose && holdCounter <= 0)
                    {
                        releaseCounter++;
                        if (releaseCounter >= releaseSamples)
                        {
                            gateOpen = false;
                            releaseCounter = 0;

                            // End clip with post-roll
                            int clipEndSample = Math.Min(totalSamples, i + postRollSamples);

                            // Check minimum length
                            if ((clipEndSample - clipStartSample) >= minClipSamples)
                            {
                                double rms = clipSampleCount > 0 ? Math.Sqrt(clipRmsSum / clipSampleCount) : 0;
                                double peakDb = clipPeak > 0 ? 20 * Math.Log10(clipPeak) : -120;
                                double rmsDb = rms > 0 ? 20 * Math.Log10(rms) : -120;

                                clips.Add(new StripSilenceClip
                                {
                                    StartTime = (double)clipStartSample / (sampleRate * channels),
                                    EndTime = (double)clipEndSample / (sampleRate * channels),
                                    PeakLevelDb = peakDb,
                                    RmsLevelDb = rmsDb
                                });
                            }
                        }
                    }
                    else
                    {
                        releaseCounter = 0;
                        if (holdCounter > 0) holdCounter--;
                        if (!shouldClose) holdCounter = holdSamples;
                    }
                }

                // Report progress periodically
                if (i % (sampleRate * channels) == 0)
                {
                    ProgressChanged?.Invoke(this, (double)i / totalSamples * 100);
                }
            }

            // Handle clip still open at end
            if (gateOpen && clipSampleCount > 0)
            {
                int clipEndSample = totalSamples;
                if ((clipEndSample - clipStartSample) >= minClipSamples)
                {
                    double rms = Math.Sqrt(clipRmsSum / clipSampleCount);
                    double peakDb = clipPeak > 0 ? 20 * Math.Log10(clipPeak) : -120;
                    double rmsDb = rms > 0 ? 20 * Math.Log10(rms) : -120;

                    clips.Add(new StripSilenceClip
                    {
                        StartTime = (double)clipStartSample / (sampleRate * channels),
                        EndTime = (double)clipEndSample / (sampleRate * channels),
                        PeakLevelDb = peakDb,
                        RmsLevelDb = rmsDb
                    });
                }
            }

            result.Clips = clips;
            result.TotalDuration = (double)totalSamples / (sampleRate * channels);
            result.NonSilentDuration = clips.Sum(c => c.Duration);

            ProgressChanged?.Invoke(this, 100);
            AnalysisComplete?.Invoke(this, result);

            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Extracts audio samples for a specific clip.
    /// </summary>
    /// <param name="sourceSamples">Source audio samples.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="clip">Clip to extract.</param>
    /// <param name="settings">Settings for fade processing.</param>
    /// <returns>Extracted audio samples.</returns>
    public float[] ExtractClipSamples(
        float[] sourceSamples,
        int sampleRate,
        int channels,
        StripSilenceClip clip,
        StripSilenceSettings? settings = null)
    {
        settings ??= new StripSilenceSettings();

        int startSample = Math.Max(0, (int)(clip.StartTime * sampleRate * channels));
        int endSample = Math.Min(sourceSamples.Length, (int)(clip.EndTime * sampleRate * channels));
        int length = endSample - startSample;

        if (length <= 0) return [];

        var samples = new float[length];
        Array.Copy(sourceSamples, startSample, samples, 0, length);

        // Apply fades
        if (settings.ApplyFadeIn || settings.ApplyFadeOut)
        {
            int fadeSamples = (int)(settings.FadeLengthMs * sampleRate / 1000.0) * channels;

            if (settings.ApplyFadeIn)
            {
                int fadeInLength = Math.Min(fadeSamples, length / 2);
                for (int i = 0; i < fadeInLength; i++)
                {
                    double fade = (double)i / fadeInLength;
                    // S-curve fade
                    fade = fade * fade * (3 - 2 * fade);
                    samples[i] *= (float)fade;
                }
            }

            if (settings.ApplyFadeOut)
            {
                int fadeOutLength = Math.Min(fadeSamples, length / 2);
                int fadeOutStart = length - fadeOutLength;
                for (int i = 0; i < fadeOutLength; i++)
                {
                    double fade = 1.0 - ((double)i / fadeOutLength);
                    // S-curve fade
                    fade = fade * fade * (3 - 2 * fade);
                    samples[fadeOutStart + i] *= (float)fade;
                }
            }
        }

        return samples;
    }

    /// <summary>
    /// Extracts all selected clips and returns them with their audio.
    /// </summary>
    /// <param name="sourceSamples">Source audio samples.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="result">Analysis result containing clips.</param>
    /// <returns>List of clips with their audio samples.</returns>
    public List<StripSilenceClip> ExtractAllClips(
        float[] sourceSamples,
        int sampleRate,
        int channels,
        StripSilenceResult result)
    {
        var extractedClips = new List<StripSilenceClip>();

        foreach (var clip in result.Clips.Where(c => c.IsSelected))
        {
            var newClip = new StripSilenceClip
            {
                Id = clip.Id,
                StartTime = clip.StartTime,
                EndTime = clip.EndTime,
                PeakLevelDb = clip.PeakLevelDb,
                RmsLevelDb = clip.RmsLevelDb,
                IsSelected = clip.IsSelected,
                Samples = ExtractClipSamples(sourceSamples, sampleRate, channels, clip, result.Settings)
            };
            extractedClips.Add(newClip);
        }

        return extractedClips;
    }

    /// <summary>
    /// Merges adjacent clips if they are close together.
    /// </summary>
    /// <param name="clips">List of clips to merge.</param>
    /// <param name="maxGapMs">Maximum gap in milliseconds to merge.</param>
    /// <returns>Merged list of clips.</returns>
    public List<StripSilenceClip> MergeAdjacentClips(List<StripSilenceClip> clips, double maxGapMs = 50)
    {
        if (clips.Count <= 1) return clips.ToList();

        var sortedClips = clips.OrderBy(c => c.StartTime).ToList();
        var mergedClips = new List<StripSilenceClip>();

        StripSilenceClip? current = null;

        foreach (var clip in sortedClips)
        {
            if (current == null)
            {
                current = new StripSilenceClip
                {
                    StartTime = clip.StartTime,
                    EndTime = clip.EndTime,
                    PeakLevelDb = clip.PeakLevelDb,
                    RmsLevelDb = clip.RmsLevelDb,
                    IsSelected = clip.IsSelected
                };
            }
            else
            {
                double gap = (clip.StartTime - current.EndTime) * 1000; // Convert to ms

                if (gap <= maxGapMs)
                {
                    // Merge clips
                    current.EndTime = clip.EndTime;
                    current.PeakLevelDb = Math.Max(current.PeakLevelDb, clip.PeakLevelDb);
                    current.RmsLevelDb = (current.RmsLevelDb + clip.RmsLevelDb) / 2;
                }
                else
                {
                    mergedClips.Add(current);
                    current = new StripSilenceClip
                    {
                        StartTime = clip.StartTime,
                        EndTime = clip.EndTime,
                        PeakLevelDb = clip.PeakLevelDb,
                        RmsLevelDb = clip.RmsLevelDb,
                        IsSelected = clip.IsSelected
                    };
                }
            }
        }

        if (current != null)
        {
            mergedClips.Add(current);
        }

        return mergedClips;
    }
}
