using System;
using FluentAssertions;
using Xunit;

namespace MusicEngineEditor.Tests.Audio;

/// <summary>
/// Optional audio smoke test to verify we can synthesize and analyze audio (dominant frequency, RMS/peak).
/// Runs only when ENABLE_AUDIO_TESTS=1 or -AudioSmoke is passed to build.ps1.
/// </summary>
public class AudioAnalysisSmokeTests
{
    [Fact]
    [Trait("Category", "Audio")]
    public void GeneratedSine_ShouldReportDominantFrequency()
    {
        if (!IsAudioEnabled())
        {
            // Skip gracefully when not requested
            return;
        }

        const int sampleRate = 48000;
        const double targetHz = 440.0;
        const double durationSeconds = 1.0;
        int sampleCount = (int)(sampleRate * durationSeconds);
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * targetHz * i / sampleRate);
        }

        var metrics = AudioAnalysisHelper.AnalyzeMono(samples, sampleRate);

        metrics.DominantFrequencyHz.Should().BeApproximately(targetHz, 5.0, "FFT should find the sine's frequency");
        metrics.Rms.Should().BeGreaterThan(0.6).And.BeLessThan(0.8); // sine RMS ~0.707
        metrics.Peak.Should().BeApproximately(1.0, 0.05);
    }

    private static bool IsAudioEnabled()
    {
        var env = Environment.GetEnvironmentVariable("ENABLE_AUDIO_TESTS");
        return string.Equals(env, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
    }
}
