// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Central performance/profile toggles so subsystems can be switched off for low-power or game-embedding scenarios.

using System;

namespace MusicEngineEditor.Services;

/// <summary>
/// Aggregates performance-related toggles. Defaults match previous behaviour so existing users/tests are unaffected.
/// Environment variables override defaults to keep the app modular without code changes:
/// - ME_PERF_PROFILE = low | balanced | high
/// - ME_DISABLE_MIDI = 1/true
/// - ME_DISABLE_SEQUENCER = 1/true
/// - ME_DISABLE_PERF_MONITOR = 1/true
/// - ME_DISABLE_INLINE_VISUALS = 1/true
/// - ME_SAMPLE_RATE = int (e.g. 44100, 48000)
/// - ME_BUFFER_SIZE = int (64-4096)
/// </summary>
public sealed record PerformanceOptions
{
    public bool EnableMidi { get; init; } = true;
    public bool StartSequencer { get; init; } = true;
    public bool EnablePerfMonitor { get; init; } = true;
    public bool EnableInlineVisuals { get; init; } = true;
    public int? SampleRate { get; init; }
    public int? BufferSize { get; init; }
    public string ProfileName { get; init; } = "balanced";
}

/// <summary>
/// Static holder for performance options.
/// </summary>
public static class PerformanceConfig
{
    public static PerformanceOptions Options { get; private set; } = new();

    public static void LoadFromEnvironment()
    {
        var profile = (Environment.GetEnvironmentVariable("ME_PERF_PROFILE") ?? "balanced").Trim().ToLowerInvariant();
        var opt = profile switch
        {
            "low" => new PerformanceOptions
            {
                ProfileName = "low",
                EnableMidi = false,
                StartSequencer = false,
                EnablePerfMonitor = false,
                EnableInlineVisuals = false,
                SampleRate = 44100,
                BufferSize = 1024
            },
            "high" => new PerformanceOptions
            {
                ProfileName = "high",
                EnableMidi = true,
                StartSequencer = true,
                EnablePerfMonitor = true,
                EnableInlineVisuals = true,
                SampleRate = 48000,
                BufferSize = 256
            },
            _ => new PerformanceOptions { ProfileName = "balanced" }
        };

        // Explicit overrides win over profile presets
        opt = opt with
        {
            EnableMidi = OverrideBool("ME_DISABLE_MIDI", opt.EnableMidi, invert: true),
            StartSequencer = OverrideBool("ME_DISABLE_SEQUENCER", opt.StartSequencer, invert: true),
            EnablePerfMonitor = OverrideBool("ME_DISABLE_PERF_MONITOR", opt.EnablePerfMonitor, invert: true),
            EnableInlineVisuals = OverrideBool("ME_DISABLE_INLINE_VISUALS", opt.EnableInlineVisuals, invert: true),
            SampleRate = OverrideInt("ME_SAMPLE_RATE", opt.SampleRate),
            BufferSize = OverrideInt("ME_BUFFER_SIZE", opt.BufferSize)
        };

        Options = opt;
    }

    private static bool OverrideBool(string envName, bool current, bool invert)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(raw)) return current;

        var parsed = raw.Equals("1", StringComparison.OrdinalIgnoreCase)
                     || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                     || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
                     || raw.Equals("on", StringComparison.OrdinalIgnoreCase);

        return invert ? !parsed : parsed;
    }

    private static int? OverrideInt(string envName, int? current)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        if (int.TryParse(raw, out var value))
        {
            return value;
        }

        return current;
    }
}
