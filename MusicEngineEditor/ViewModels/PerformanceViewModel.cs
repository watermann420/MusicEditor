// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for performance monitor.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Represents a single data point in the CPU usage history graph.
/// </summary>
public class CpuDataPoint
{
    public DateTime Timestamp { get; init; }
    public double Value { get; init; }
}

/// <summary>
/// Represents per-plugin CPU usage information.
/// </summary>
public partial class PluginCpuInfo : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private string _cpuUsageFormatted = "0.0%";

    partial void OnCpuUsageChanged(double value)
    {
        CpuUsageFormatted = $"{value:F1}%";
    }
}

/// <summary>
/// ViewModel for performance monitoring display and controls.
/// </summary>
public partial class PerformanceViewModel : ViewModelBase
{
    #region Private Fields

    private readonly PerformanceMonitorService _monitorService;
    private const int MaxHistoryPoints = 300; // 30 seconds at 10Hz

    #endregion

    #region Observable Properties

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private double _peakCpuUsage;

    [ObservableProperty]
    private double _averageCpuUsage;

    [ObservableProperty]
    private double _memoryUsageMB;

    [ObservableProperty]
    private double _peakMemoryUsageMB;

    [ObservableProperty]
    private int _dropoutCount;

    [ObservableProperty]
    private double _diskActivity;

    [ObservableProperty]
    private int _bufferSize = 512;

    [ObservableProperty]
    private int _sampleRate = 44100;

    [ObservableProperty]
    private double _latencyMs;

    [ObservableProperty]
    private bool _isMonitoring;

    #endregion

    #region Formatted Display Properties

    public string CpuUsageFormatted => $"{CpuUsage:F1}%";
    public string PeakCpuUsageFormatted => $"{PeakCpuUsage:F1}%";
    public string AverageCpuUsageFormatted => $"{AverageCpuUsage:F1}%";
    public string MemoryUsageFormatted => $"{MemoryUsageMB:F0} MB";
    public string PeakMemoryUsageFormatted => $"{PeakMemoryUsageMB:F0} MB";
    public string DropoutCountFormatted => DropoutCount == 0 ? "0" : DropoutCount.ToString();
    public string DiskActivityFormatted => $"{DiskActivity:F0}%";
    public string LatencyFormatted => $"{LatencyMs:F1} ms";
    public string BufferSizeFormatted => $"{BufferSize} samples";
    public string SampleRateFormatted => $"{SampleRate} Hz";

    /// <summary>
    /// Compact status string for status bar display.
    /// </summary>
    public string CompactStatus => $"CPU: {CpuUsage:F0}% | MEM: {MemoryUsageMB:F0} MB";

    /// <summary>
    /// Detailed tooltip text.
    /// </summary>
    public string DetailedTooltip => $"CPU Usage: {CpuUsage:F1}% (Peak: {PeakCpuUsage:F1}%, Avg: {AverageCpuUsage:F1}%)\n" +
                                      $"Memory: {MemoryUsageMB:F1} MB (Peak: {PeakMemoryUsageMB:F1} MB)\n" +
                                      $"Buffer Underruns: {DropoutCount}\n" +
                                      $"Audio Latency: {LatencyMs:F1} ms\n" +
                                      $"Buffer Size: {BufferSize} samples @ {SampleRate} Hz\n" +
                                      "Click for detailed performance view";

    #endregion

    #region Collections

    /// <summary>
    /// CPU usage history for graphing (last 30 seconds).
    /// </summary>
    public ObservableCollection<CpuDataPoint> CpuHistory { get; } = new();

    /// <summary>
    /// Per-plugin CPU usage breakdown.
    /// </summary>
    public ObservableCollection<PluginCpuInfo> PluginCpuUsage { get; } = new();

    #endregion

    #region Constructor

    public PerformanceViewModel(PerformanceMonitorService monitorService)
    {
        _monitorService = monitorService;

        // Subscribe to service events
        _monitorService.PerformanceUpdated += OnPerformanceUpdated;
        _monitorService.DropoutDetected += OnDropoutDetected;

        // Initialize with current values
        UpdateFromService();
    }

    /// <summary>
    /// Parameterless constructor for design-time support.
    /// </summary>
    public PerformanceViewModel() : this(new PerformanceMonitorService())
    {
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void ResetStats()
    {
        _monitorService.ResetStats();
        CpuHistory.Clear();
        UpdateFromService();
    }

    [RelayCommand]
    private void ToggleMonitoring()
    {
        if (IsMonitoring)
        {
            _monitorService.Stop();
        }
        else
        {
            _monitorService.Start();
        }
        IsMonitoring = _monitorService.IsMonitoring;
    }

    [RelayCommand]
    private void StartMonitoring()
    {
        _monitorService.Start();
        IsMonitoring = _monitorService.IsMonitoring;
    }

    [RelayCommand]
    private void StopMonitoring()
    {
        _monitorService.Stop();
        IsMonitoring = _monitorService.IsMonitoring;
    }

    #endregion

    #region Event Handlers

    private void OnPerformanceUpdated(object? sender, PerformanceUpdateEventArgs e)
    {
        UpdateFromService();

        // Add to history
        var dataPoint = new CpuDataPoint
        {
            Timestamp = DateTime.Now,
            Value = e.CpuUsage
        };

        CpuHistory.Add(dataPoint);

        // Trim old data points
        while (CpuHistory.Count > MaxHistoryPoints)
        {
            CpuHistory.RemoveAt(0);
        }
    }

    private void OnDropoutDetected(object? sender, DropoutDetectedEventArgs e)
    {
        DropoutCount = e.TotalDropouts;
        OnPropertyChanged(nameof(DropoutCountFormatted));
    }

    #endregion

    #region Private Methods

    private void UpdateFromService()
    {
        CpuUsage = _monitorService.CpuUsage;
        PeakCpuUsage = _monitorService.PeakCpuUsage;
        AverageCpuUsage = _monitorService.AverageCpuUsage;
        MemoryUsageMB = _monitorService.MemoryUsageMB;
        PeakMemoryUsageMB = _monitorService.PeakMemoryUsageMB;
        DropoutCount = _monitorService.BufferUnderruns;
        DiskActivity = _monitorService.DiskActivity;
        BufferSize = _monitorService.BufferSize;
        SampleRate = _monitorService.SampleRate;
        LatencyMs = _monitorService.LatencyMs;
        IsMonitoring = _monitorService.IsMonitoring;

        // Notify formatted property changes
        OnPropertyChanged(nameof(CpuUsageFormatted));
        OnPropertyChanged(nameof(PeakCpuUsageFormatted));
        OnPropertyChanged(nameof(AverageCpuUsageFormatted));
        OnPropertyChanged(nameof(MemoryUsageFormatted));
        OnPropertyChanged(nameof(PeakMemoryUsageFormatted));
        OnPropertyChanged(nameof(DropoutCountFormatted));
        OnPropertyChanged(nameof(DiskActivityFormatted));
        OnPropertyChanged(nameof(LatencyFormatted));
        OnPropertyChanged(nameof(BufferSizeFormatted));
        OnPropertyChanged(nameof(SampleRateFormatted));
        OnPropertyChanged(nameof(CompactStatus));
        OnPropertyChanged(nameof(DetailedTooltip));
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates audio settings from the engine.
    /// </summary>
    public void UpdateAudioSettings(int bufferSize, int sampleRate)
    {
        _monitorService.UpdateAudioSettings(bufferSize, sampleRate);
        BufferSize = bufferSize;
        SampleRate = sampleRate;
        LatencyMs = _monitorService.LatencyMs;
        OnPropertyChanged(nameof(BufferSizeFormatted));
        OnPropertyChanged(nameof(SampleRateFormatted));
        OnPropertyChanged(nameof(LatencyFormatted));
        OnPropertyChanged(nameof(DetailedTooltip));
    }

    /// <summary>
    /// Reports a dropout from the audio engine.
    /// </summary>
    public void ReportDropout(string? message = null)
    {
        _monitorService.ReportDropout(message);
    }

    /// <summary>
    /// Updates plugin CPU usage (for per-plugin breakdown).
    /// </summary>
    public void UpdatePluginCpuUsage(string pluginName, double cpuUsage)
    {
        var existing = PluginCpuUsage.FirstOrDefault(p => p.Name == pluginName);
        if (existing != null)
        {
            existing.CpuUsage = cpuUsage;
        }
        else
        {
            PluginCpuUsage.Add(new PluginCpuInfo
            {
                Name = pluginName,
                CpuUsage = cpuUsage
            });
        }
    }

    /// <summary>
    /// Clears plugin CPU usage data.
    /// </summary>
    public void ClearPluginCpuUsage()
    {
        PluginCpuUsage.Clear();
    }

    /// <summary>
    /// Gets the underlying monitor service for direct access.
    /// </summary>
    public PerformanceMonitorService GetService() => _monitorService;

    #endregion
}
