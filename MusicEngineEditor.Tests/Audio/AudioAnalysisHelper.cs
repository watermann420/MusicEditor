using System;
using System.Linq;
using MathNet.Numerics.IntegralTransforms;

namespace MusicEngineEditor.Tests.Audio;

/// <summary>
/// Lightweight audio analysis helpers for tests (RMS, peak, dominant frequency).
/// </summary>
public static class AudioAnalysisHelper
{
    public static AudioMetrics AnalyzeMono(float[] samples, int sampleRate)
    {
        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples must not be empty", nameof(samples));
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));

        // RMS / peak
        double rms = Math.Sqrt(samples.Select(s => s * s).Average());
        float peak = samples.Select(Math.Abs).Max();

        // Dominant frequency via simple FFT
        int fftSize = NextPowerOfTwo(samples.Length);
        var buffer = new System.Numerics.Complex[fftSize];
        for (int i = 0; i < samples.Length; i++)
        {
            buffer[i] = new System.Numerics.Complex(samples[i], 0);
        }

        Fourier.Forward(buffer, FourierOptions.Matlab);

        // Magnitude spectrum (ignore DC)
        var magnitudes = buffer
            .Take(fftSize / 2)
            .Select(c => c.Magnitude)
            .ToArray();

        int peakIndex = 1;
        double peakMag = magnitudes[1];
        for (int i = 2; i < magnitudes.Length; i++)
        {
            if (magnitudes[i] > peakMag)
            {
                peakMag = magnitudes[i];
                peakIndex = i;
            }
        }

        double binWidth = (double)sampleRate / fftSize;
        double dominantHz = peakIndex * binWidth;

        return new AudioMetrics
        {
            Rms = rms,
            Peak = peak,
            DominantFrequencyHz = dominantHz
        };
    }

    private static int NextPowerOfTwo(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }
}

public record AudioMetrics
{
    public double Rms { get; init; }
    public double Peak { get; init; }
    public double DominantFrequencyHz { get; init; }
}
