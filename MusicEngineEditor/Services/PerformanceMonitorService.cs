using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace MusicEngineEditor.Services;

/// <summary>
/// Event args for performance update events.
/// </summary>
public class PerformanceUpdateEventArgs : EventArgs
{
    public double CpuUsage { get; init; }
    public double MemoryUsageMB { get; init; }
    public int BufferUnderruns { get; init; }
    public double DiskActivity { get; init; }
}

/// <summary>
/// Event args for dropout detection events.
/// </summary>
public class DropoutDetectedEventArgs : EventArgs
{
    public DateTime Timestamp { get; init; }
    public int TotalDropouts { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Service for monitoring CPU, memory, and audio performance metrics.
/// Updates at approximately 10Hz (every 100ms) for responsive UI feedback.
/// </summary>
public class PerformanceMonitorService : IDisposable
{
    #region Constants

    private const int UpdateIntervalMs = 100; // 10Hz update rate
    private const int CpuSampleCount = 10; // Number of samples for CPU averaging
    private const double DropoutThresholdPercent = 90.0; // CPU threshold for dropout warning

    #endregion

    #region Events

    /// <summary>
    /// Raised when performance metrics are updated (~10Hz).
    /// </summary>
    public event EventHandler<PerformanceUpdateEventArgs>? PerformanceUpdated;

    /// <summary>
    /// Raised when a buffer underrun/dropout is detected.
    /// </summary>
    public event EventHandler<DropoutDetectedEventArgs>? DropoutDetected;

    #endregion

    #region Properties

    /// <summary>
    /// Current CPU usage (0-100%).
    /// </summary>
    public double CpuUsage { get; private set; }

    /// <summary>
    /// Peak CPU usage since last reset (0-100%).
    /// </summary>
    public double PeakCpuUsage { get; private set; }

    /// <summary>
    /// Average CPU usage over the sample window (0-100%).
    /// </summary>
    public double AverageCpuUsage { get; private set; }

    /// <summary>
    /// Current memory usage in megabytes.
    /// </summary>
    public double MemoryUsageMB { get; private set; }

    /// <summary>
    /// Peak memory usage since last reset in megabytes.
    /// </summary>
    public double PeakMemoryUsageMB { get; private set; }

    /// <summary>
    /// Number of detected buffer underruns/dropouts.
    /// </summary>
    public int BufferUnderruns { get; private set; }

    /// <summary>
    /// Current disk activity level (0-100%).
    /// </summary>
    public double DiskActivity { get; private set; }

    /// <summary>
    /// Whether monitoring is currently active.
    /// </summary>
    public bool IsMonitoring { get; private set; }

    /// <summary>
    /// Current audio buffer size in samples.
    /// </summary>
    public int BufferSize { get; set; } = 512;

    /// <summary>
    /// Current audio sample rate in Hz.
    /// </summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Calculated audio latency in milliseconds.
    /// </summary>
    public double LatencyMs => BufferSize > 0 && SampleRate > 0
        ? (double)BufferSize / SampleRate * 1000.0
        : 0.0;

    #endregion

    #region Private Fields

    private readonly Process _currentProcess;
    private readonly DispatcherTimer _updateTimer;
    private readonly Stopwatch _cpuStopwatch;
    private readonly double[] _cpuSamples;
    private int _cpuSampleIndex;
    private TimeSpan _lastProcessorTime;
    private DateTime _lastSampleTime;
    private bool _disposed;

    // For disk activity simulation (would need actual I/O counters in production)
    private readonly PerformanceCounter? _diskCounter;

    #endregion

    #region Constructor

    public PerformanceMonitorService()
    {
        _currentProcess = Process.GetCurrentProcess();
        _cpuStopwatch = Stopwatch.StartNew();
        _cpuSamples = new double[CpuSampleCount];
        _cpuSampleIndex = 0;
        _lastProcessorTime = _currentProcess.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;

        // Try to create disk performance counter (may fail on some systems)
        try
        {
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true);
        }
        catch
        {
            _diskCounter = null;
        }

        _updateTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(UpdateIntervalMs)
        };
        _updateTimer.Tick += OnUpdateTimerTick;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts performance monitoring.
    /// </summary>
    public void Start()
    {
        if (IsMonitoring) return;

        _lastProcessorTime = _currentProcess.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;
        _cpuStopwatch.Restart();

        IsMonitoring = true;
        _updateTimer.Start();
    }

    /// <summary>
    /// Stops performance monitoring.
    /// </summary>
    public void Stop()
    {
        if (!IsMonitoring) return;

        _updateTimer.Stop();
        IsMonitoring = false;
    }

    /// <summary>
    /// Resets all statistics (peak values, averages, dropout count).
    /// </summary>
    public void ResetStats()
    {
        PeakCpuUsage = 0;
        PeakMemoryUsageMB = 0;
        BufferUnderruns = 0;
        Array.Clear(_cpuSamples, 0, _cpuSamples.Length);
        _cpuSampleIndex = 0;
        AverageCpuUsage = 0;
    }

    /// <summary>
    /// Reports a buffer underrun/dropout from the audio engine.
    /// </summary>
    public void ReportDropout(string? message = null)
    {
        BufferUnderruns++;
        DropoutDetected?.Invoke(this, new DropoutDetectedEventArgs
        {
            Timestamp = DateTime.Now,
            TotalDropouts = BufferUnderruns,
            Message = message ?? $"Audio buffer underrun detected (total: {BufferUnderruns})"
        });
    }

    /// <summary>
    /// Updates buffer size and sample rate from audio engine settings.
    /// </summary>
    public void UpdateAudioSettings(int bufferSize, int sampleRate)
    {
        BufferSize = bufferSize;
        SampleRate = sampleRate;
    }

    #endregion

    #region Private Methods

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        UpdateCpuUsage();
        UpdateMemoryUsage();
        UpdateDiskActivity();
        CheckForDropouts();

        // Raise the update event
        PerformanceUpdated?.Invoke(this, new PerformanceUpdateEventArgs
        {
            CpuUsage = CpuUsage,
            MemoryUsageMB = MemoryUsageMB,
            BufferUnderruns = BufferUnderruns,
            DiskActivity = DiskActivity
        });
    }

    private void UpdateCpuUsage()
    {
        try
        {
            _currentProcess.Refresh();

            var currentTime = DateTime.UtcNow;
            var currentProcessorTime = _currentProcess.TotalProcessorTime;

            var timeDelta = (currentTime - _lastSampleTime).TotalMilliseconds;
            if (timeDelta > 0)
            {
                var cpuDelta = (currentProcessorTime - _lastProcessorTime).TotalMilliseconds;
                var cpuUsage = cpuDelta / timeDelta / Environment.ProcessorCount * 100.0;

                // Clamp to valid range
                CpuUsage = Math.Max(0, Math.Min(100, cpuUsage));

                // Update peak
                if (CpuUsage > PeakCpuUsage)
                {
                    PeakCpuUsage = CpuUsage;
                }

                // Update rolling average
                _cpuSamples[_cpuSampleIndex] = CpuUsage;
                _cpuSampleIndex = (_cpuSampleIndex + 1) % CpuSampleCount;

                double sum = 0;
                int count = 0;
                for (int i = 0; i < CpuSampleCount; i++)
                {
                    if (_cpuSamples[i] > 0 || i < _cpuSampleIndex)
                    {
                        sum += _cpuSamples[i];
                        count++;
                    }
                }
                AverageCpuUsage = count > 0 ? sum / count : 0;
            }

            _lastProcessorTime = currentProcessorTime;
            _lastSampleTime = currentTime;
        }
        catch
        {
            // Ignore errors reading CPU usage
        }
    }

    private void UpdateMemoryUsage()
    {
        try
        {
            _currentProcess.Refresh();
            MemoryUsageMB = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);

            if (MemoryUsageMB > PeakMemoryUsageMB)
            {
                PeakMemoryUsageMB = MemoryUsageMB;
            }
        }
        catch
        {
            // Ignore errors reading memory
        }
    }

    private void UpdateDiskActivity()
    {
        try
        {
            if (_diskCounter != null)
            {
                DiskActivity = Math.Max(0, Math.Min(100, _diskCounter.NextValue()));
            }
            else
            {
                DiskActivity = 0;
            }
        }
        catch
        {
            DiskActivity = 0;
        }
    }

    private void CheckForDropouts()
    {
        // Auto-detect potential dropouts based on high CPU usage
        // This is a heuristic; real dropout detection would come from the audio engine
        if (CpuUsage > DropoutThresholdPercent)
        {
            // Don't auto-report, just log the warning state
            // Real dropouts should be reported via ReportDropout() from the audio engine
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        _updateTimer.Tick -= OnUpdateTimerTick;
        _diskCounter?.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    #endregion
}
