using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace MusicEngineEditor.Tests.Audio;

/// <summary>
/// Pattern-oriented audio smoke test: synthesizes a short note pattern (MIDI-like) and validates
/// per-step dominant frequency and RMS levels. Runs when ENABLE_AUDIO_TESTS=1 or -AudioSmoke is used.
/// </summary>
public class PatternSmokeTests
{
    private record NoteStep(double Hz, double DurationSeconds, double Amplitude = 0.9);

    [Fact]
    [Trait("Category", "Audio")]
    public void SinePattern_ShouldHitExpectedFrequenciesAndLevels()
    {
        if (!IsAudioEnabled())
            return; // skip silently unless enabled

        const int sampleRate = 48000;
        var pattern = new List<NoteStep>
        {
            new(440.0, 0.25),   // A4
            new(523.25, 0.25),  // C5
            new(659.25, 0.25),  // E5
            new(880.0, 0.25)    // A5
        };

        // synthesize concatenated buffer
        var samples = SynthesizePattern(pattern, sampleRate);

        // verify each segment
        int offset = 0;
        foreach (var step in pattern)
        {
            int length = (int)(step.DurationSeconds * sampleRate);
            var segment = samples.Skip(offset).Take(length).ToArray();
            offset += length;

            var metrics = AudioAnalysisHelper.AnalyzeMono(segment, sampleRate);
            metrics.DominantFrequencyHz.Should().BeApproximately(step.Hz, 6.0, $"dominant Hz should match {step.Hz}");
            metrics.Rms.Should().BeGreaterThan(0.5).And.BeLessThanOrEqualTo(step.Amplitude * 0.8);
        }
    }

    private static float[] SynthesizePattern(IEnumerable<NoteStep> pattern, int sampleRate)
    {
        var totalSamples = pattern.Sum(p => (int)(p.DurationSeconds * sampleRate));
        var buffer = new float[totalSamples];
        int index = 0;

        foreach (var step in pattern)
        {
            int stepSamples = (int)(step.DurationSeconds * sampleRate);
            for (int i = 0; i < stepSamples; i++)
            {
                buffer[index++] = (float)(step.Amplitude * Math.Sin(2 * Math.PI * step.Hz * i / sampleRate));
            }
        }

        return buffer;
    }

    private static bool IsAudioEnabled()
    {
        var env = Environment.GetEnvironmentVariable("ENABLE_AUDIO_TESTS");
        return string.Equals(env, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
    }
}
