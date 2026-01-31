using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MusicEngineEditor.Services;
using Xunit;

namespace MusicEngineEditor.Tests.Perf;

/// <summary>
/// Lightweight performance & memory smoke checks. Only run when ENABLE_PERF_TESTS=1 or -PerfSmoke is used.
/// Thresholds are generous to catch regressions without being flaky.
/// </summary>
public class PerformanceSmokeTests : IDisposable
{
    private readonly string _tempRoot;

    public PerformanceSmokeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "MusicEngineEditorPerf", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task ProjectCreation_ShouldStayFast_And_LowMemory()
    {
        if (!IsPerfEnabled()) return;

        var service = new ProjectService();
        var sw = Stopwatch.StartNew();

        // Create several projects to simulate typical usage
        for (int i = 0; i < 6; i++)
        {
            var name = $"PerfProj{i}";
            await service.CreateProjectAsync(name, _tempRoot);
        }

        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(2200, "multiple project creations should remain reasonably fast");

        // Rough memory check after GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var mem = GC.GetTotalMemory(forceFullCollection: true);
        mem.Should().BeLessThan(300 * 1024 * 1024, "perf run should not bloat managed heap (>300MB)");
    }

    [Fact]
    [Trait("Category", "Perf")]
    public void AudioAnalysis_ShouldStayFast()
    {
        if (!IsPerfEnabled()) return;

        const int sampleRate = 48000;
        const double hz = 220.0;
        const double duration = 5.0; // 5 seconds buffer
        int samplesCount = (int)(sampleRate * duration);
        var samples = new float[samplesCount];

        for (int i = 0; i < samplesCount; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * hz * i / sampleRate);

        var sw = Stopwatch.StartNew();
        var metrics = Audio.AudioAnalysisHelper.AnalyzeMono(samples, sampleRate);
        sw.Stop();

        metrics.DominantFrequencyHz.Should().BeApproximately(hz, 6.0);
        sw.ElapsedMilliseconds.Should().BeLessThan(600, "FFT on 5s buffer should stay sub-600ms on typical dev hardware");
    }

    private static bool IsPerfEnabled()
    {
        var env = Environment.GetEnvironmentVariable("ENABLE_PERF_TESTS");
        return string.Equals(env, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
