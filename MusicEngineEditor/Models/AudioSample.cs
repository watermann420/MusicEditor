using System;
using System.Collections.Generic;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents an audio sample within a sound pack
/// </summary>
public class AudioSample
{
    /// <summary>
    /// Unique identifier for the sample
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the sample
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Category of the sample (e.g., Drums, Bass, Synths, FX)
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Relative file path within the sound pack
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// BPM (beats per minute) of the sample, if applicable
    /// </summary>
    public double? Bpm { get; set; }

    /// <summary>
    /// Musical key of the sample, if applicable (e.g., "C", "Am", "F#m")
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Duration of the audio sample
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Tags for searching and filtering samples
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Reference to the parent sound pack ID
    /// </summary>
    public string? SoundPackId { get; set; }

    /// <summary>
    /// Gets the duration formatted as mm:ss or ss.fff for short samples
    /// </summary>
    public string DurationFormatted
    {
        get
        {
            if (Duration.TotalSeconds < 1)
                return $"{Duration.TotalMilliseconds:F0}ms";
            if (Duration.TotalMinutes < 1)
                return $"{Duration.TotalSeconds:F1}s";
            return $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}";
        }
    }
}
