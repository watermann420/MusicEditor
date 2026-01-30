// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using NAudio.Wave;

namespace MusicEngineEditor.Services;

/// <summary>
/// Render format options.
/// </summary>
public enum RenderFormat
{
    /// <summary>WAV format (uncompressed PCM).</summary>
    Wav,
    /// <summary>MP3 format (lossy compression).</summary>
    Mp3,
    /// <summary>FLAC format (lossless compression).</summary>
    Flac,
    /// <summary>OGG Vorbis format (lossy compression).</summary>
    Ogg,
    /// <summary>AIFF format (uncompressed PCM).</summary>
    Aiff
}

/// <summary>
/// Type of item to render.
/// </summary>
public enum RenderItemType
{
    /// <summary>Full mixdown of all tracks.</summary>
    Mixdown,
    /// <summary>Single track stem.</summary>
    Stem,
    /// <summary>Bus output.</summary>
    Bus,
    /// <summary>Selected region only.</summary>
    Region,
    /// <summary>Loop region.</summary>
    Loop
}

/// <summary>
/// Status of a render item.
/// </summary>
public enum RenderStatus
{
    /// <summary>Waiting to be processed.</summary>
    Pending,
    /// <summary>Currently being rendered.</summary>
    Processing,
    /// <summary>Render completed successfully.</summary>
    Completed,
    /// <summary>Render failed with error.</summary>
    Failed,
    /// <summary>Render was cancelled.</summary>
    Cancelled,
    /// <summary>Render is paused.</summary>
    Paused
}

/// <summary>
/// Represents an item in the render queue.
/// </summary>
public class RenderQueueItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _outputPath = string.Empty;
    private RenderFormat _format = RenderFormat.Wav;
    private RenderItemType _type = RenderItemType.Mixdown;
    private double _progress;
    private RenderStatus _status = RenderStatus.Pending;
    private string? _errorMessage;
    private TimeSpan _estimatedTime;
    private TimeSpan _elapsedTime;
    private long _outputFileSize;

    /// <summary>
    /// Gets the unique identifier for this render item.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the display name of the render item.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    /// <summary>
    /// Gets or sets the output file path.
    /// </summary>
    public string OutputPath
    {
        get => _outputPath;
        set { _outputPath = value; OnPropertyChanged(nameof(OutputPath)); }
    }

    /// <summary>
    /// Gets or sets the output format.
    /// </summary>
    public RenderFormat Format
    {
        get => _format;
        set { _format = value; OnPropertyChanged(nameof(Format)); OnPropertyChanged(nameof(FormatString)); }
    }

    /// <summary>
    /// Gets the format as a display string.
    /// </summary>
    public string FormatString => Format.ToString().ToUpper();

    /// <summary>
    /// Gets or sets the render item type.
    /// </summary>
    public RenderItemType Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(nameof(Type)); }
    }

    /// <summary>
    /// Gets or sets the render progress (0-100).
    /// </summary>
    public double Progress
    {
        get => _progress;
        set { _progress = Math.Clamp(value, 0, 100); OnPropertyChanged(nameof(Progress)); }
    }

    /// <summary>
    /// Gets or sets the render status.
    /// </summary>
    public RenderStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusString)); }
    }

    /// <summary>
    /// Gets the status as a display string.
    /// </summary>
    public string StatusString => Status switch
    {
        RenderStatus.Pending => "Pending",
        RenderStatus.Processing => $"Rendering... {Progress:F0}%",
        RenderStatus.Completed => "Completed",
        RenderStatus.Failed => "Failed",
        RenderStatus.Cancelled => "Cancelled",
        RenderStatus.Paused => "Paused",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets or sets the error message if render failed.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
    }

    /// <summary>
    /// Gets or sets the bit depth for rendering.
    /// </summary>
    public int BitDepth { get; set; } = 24;

    /// <summary>
    /// Gets or sets the sample rate for rendering.
    /// </summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets or sets the MP3 bitrate (kbps) if applicable.
    /// </summary>
    public int Mp3Bitrate { get; set; } = 320;

    /// <summary>
    /// Gets or sets the start time in seconds.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time in seconds.
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration in seconds.
    /// </summary>
    public double Duration => EndTime - StartTime;

    /// <summary>
    /// Gets or sets the estimated time remaining.
    /// </summary>
    public TimeSpan EstimatedTime
    {
        get => _estimatedTime;
        set { _estimatedTime = value; OnPropertyChanged(nameof(EstimatedTime)); OnPropertyChanged(nameof(EstimatedTimeString)); }
    }

    /// <summary>
    /// Gets the estimated time as a formatted string.
    /// </summary>
    public string EstimatedTimeString => EstimatedTime.TotalSeconds > 0
        ? $"~{EstimatedTime:mm\\:ss}"
        : "--:--";

    /// <summary>
    /// Gets or sets the elapsed render time.
    /// </summary>
    public TimeSpan ElapsedTime
    {
        get => _elapsedTime;
        set { _elapsedTime = value; OnPropertyChanged(nameof(ElapsedTime)); }
    }

    /// <summary>
    /// Gets or sets the output file size in bytes.
    /// </summary>
    public long OutputFileSize
    {
        get => _outputFileSize;
        set { _outputFileSize = value; OnPropertyChanged(nameof(OutputFileSize)); OnPropertyChanged(nameof(FileSizeString)); }
    }

    /// <summary>
    /// Gets the file size as a formatted string.
    /// </summary>
    public string FileSizeString
    {
        get
        {
            if (OutputFileSize == 0) return "--";
            if (OutputFileSize < 1024) return $"{OutputFileSize} B";
            if (OutputFileSize < 1024 * 1024) return $"{OutputFileSize / 1024.0:F1} KB";
            return $"{OutputFileSize / (1024.0 * 1024.0):F1} MB";
        }
    }

    /// <summary>
    /// Gets or sets the audio source to render.
    /// </summary>
    public ISampleProvider? Source { get; set; }

    /// <summary>
    /// Gets or sets whether to normalize the output.
    /// </summary>
    public bool Normalize { get; set; }

    /// <summary>
    /// Gets or sets the normalization target in dB.
    /// </summary>
    public double NormalizeTargetDb { get; set; } = -1.0;

    /// <summary>
    /// Gets or sets whether to add dithering.
    /// </summary>
    public bool ApplyDither { get; set; } = true;

    /// <summary>
    /// Gets or sets optional metadata tags.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Service for managing a queue of render tasks.
/// </summary>
public class RenderQueueService
{
    private readonly ObservableCollection<RenderQueueItem> _items = [];
    private CancellationTokenSource? _cancellationSource;
    private bool _isProcessing;
    private bool _isPaused;
    private RenderQueueItem? _currentItem;
    private DateTime _queueStartTime;

    /// <summary>
    /// Gets the render queue items.
    /// </summary>
    public ObservableCollection<RenderQueueItem> Items => _items;

    /// <summary>
    /// Gets whether the queue is currently processing.
    /// </summary>
    public bool IsProcessing => _isProcessing;

    /// <summary>
    /// Gets whether the queue is paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Gets the currently processing item.
    /// </summary>
    public RenderQueueItem? CurrentItem => _currentItem;

    /// <summary>
    /// Gets the total number of items in the queue.
    /// </summary>
    public int TotalItems => _items.Count;

    /// <summary>
    /// Gets the number of completed items.
    /// </summary>
    public int CompletedItems => _items.Count(i => i.Status == RenderStatus.Completed);

    /// <summary>
    /// Gets the number of pending items.
    /// </summary>
    public int PendingItems => _items.Count(i => i.Status == RenderStatus.Pending);

    /// <summary>
    /// Gets the overall progress (0-100).
    /// </summary>
    public double OverallProgress
    {
        get
        {
            if (_items.Count == 0) return 0;
            var completedWeight = CompletedItems * 100.0;
            var currentWeight = _currentItem?.Progress ?? 0;
            return (completedWeight + currentWeight) / _items.Count;
        }
    }

    /// <summary>
    /// Gets the estimated total time remaining.
    /// </summary>
    public TimeSpan EstimatedTotalTime
    {
        get
        {
            var remaining = _items.Where(i => i.Status == RenderStatus.Pending).Sum(i => i.EstimatedTime.TotalSeconds);
            if (_currentItem != null)
            {
                remaining += _currentItem.EstimatedTime.TotalSeconds * (1 - _currentItem.Progress / 100.0);
            }
            return TimeSpan.FromSeconds(remaining);
        }
    }

    /// <summary>
    /// Fired when an item starts processing.
    /// </summary>
    public event EventHandler<RenderQueueItem>? ItemStarted;

    /// <summary>
    /// Fired when an item completes.
    /// </summary>
    public event EventHandler<RenderQueueItem>? ItemCompleted;

    /// <summary>
    /// Fired when an item fails.
    /// </summary>
    public event EventHandler<RenderQueueItem>? ItemFailed;

    /// <summary>
    /// Fired when progress updates.
    /// </summary>
    public event EventHandler<double>? ProgressChanged;

    /// <summary>
    /// Fired when the entire queue completes.
    /// </summary>
    public event EventHandler? QueueCompleted;

    /// <summary>
    /// Fired when the queue is cancelled.
    /// </summary>
    public event EventHandler? QueueCancelled;

    /// <summary>
    /// Adds an item to the render queue.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void AddItem(RenderQueueItem item)
    {
        _items.Add(item);
        EstimateItemRenderTime(item);
    }

    /// <summary>
    /// Removes an item from the render queue.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    public void RemoveItem(RenderQueueItem item)
    {
        if (item.Status != RenderStatus.Processing)
        {
            _items.Remove(item);
        }
    }

    /// <summary>
    /// Clears completed and failed items from the queue.
    /// </summary>
    public void ClearCompleted()
    {
        var toRemove = _items.Where(i =>
            i.Status == RenderStatus.Completed ||
            i.Status == RenderStatus.Failed ||
            i.Status == RenderStatus.Cancelled).ToList();

        foreach (var item in toRemove)
        {
            _items.Remove(item);
        }
    }

    /// <summary>
    /// Clears all items from the queue.
    /// </summary>
    public void ClearAll()
    {
        if (_isProcessing)
        {
            Cancel();
        }
        _items.Clear();
    }

    /// <summary>
    /// Moves an item up in the queue.
    /// </summary>
    /// <param name="item">The item to move.</param>
    public void MoveUp(RenderQueueItem item)
    {
        var index = _items.IndexOf(item);
        if (index > 0 && item.Status == RenderStatus.Pending)
        {
            _items.Move(index, index - 1);
        }
    }

    /// <summary>
    /// Moves an item down in the queue.
    /// </summary>
    /// <param name="item">The item to move.</param>
    public void MoveDown(RenderQueueItem item)
    {
        var index = _items.IndexOf(item);
        if (index < _items.Count - 1 && item.Status == RenderStatus.Pending)
        {
            _items.Move(index, index + 1);
        }
    }

    /// <summary>
    /// Starts processing the render queue.
    /// </summary>
    public async Task StartAsync()
    {
        if (_isProcessing) return;

        _isProcessing = true;
        _isPaused = false;
        _cancellationSource = new CancellationTokenSource();
        _queueStartTime = DateTime.Now;

        try
        {
            foreach (var item in _items.Where(i => i.Status == RenderStatus.Pending))
            {
                if (_cancellationSource.IsCancellationRequested)
                    break;

                while (_isPaused && !_cancellationSource.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }

                if (_cancellationSource.IsCancellationRequested)
                    break;

                _currentItem = item;
                await ProcessItemAsync(item, _cancellationSource.Token);
            }

            if (!_cancellationSource.IsCancellationRequested)
            {
                QueueCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _isProcessing = false;
            _currentItem = null;
            _cancellationSource?.Dispose();
            _cancellationSource = null;
        }
    }

    /// <summary>
    /// Pauses the render queue.
    /// </summary>
    public void Pause()
    {
        if (_isProcessing && !_isPaused)
        {
            _isPaused = true;
            if (_currentItem != null)
            {
                _currentItem.Status = RenderStatus.Paused;
            }
        }
    }

    /// <summary>
    /// Resumes the render queue.
    /// </summary>
    public void Resume()
    {
        if (_isProcessing && _isPaused)
        {
            _isPaused = false;
            if (_currentItem != null)
            {
                _currentItem.Status = RenderStatus.Processing;
            }
        }
    }

    /// <summary>
    /// Cancels the render queue.
    /// </summary>
    public void Cancel()
    {
        _cancellationSource?.Cancel();

        foreach (var item in _items.Where(i => i.Status == RenderStatus.Pending || i.Status == RenderStatus.Paused))
        {
            item.Status = RenderStatus.Cancelled;
        }

        if (_currentItem != null)
        {
            _currentItem.Status = RenderStatus.Cancelled;
        }

        QueueCancelled?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Creates a render item for a mixdown.
    /// </summary>
    public RenderQueueItem CreateMixdownItem(string name, string outputPath, ISampleProvider source, double duration)
    {
        return new RenderQueueItem
        {
            Name = name,
            OutputPath = outputPath,
            Source = source,
            Type = RenderItemType.Mixdown,
            EndTime = duration
        };
    }

    /// <summary>
    /// Creates a render item for a stem.
    /// </summary>
    public RenderQueueItem CreateStemItem(string trackName, string outputPath, ISampleProvider source, double duration)
    {
        return new RenderQueueItem
        {
            Name = $"Stem: {trackName}",
            OutputPath = outputPath,
            Source = source,
            Type = RenderItemType.Stem,
            EndTime = duration
        };
    }

    private async Task ProcessItemAsync(RenderQueueItem item, CancellationToken cancellationToken)
    {
        item.Status = RenderStatus.Processing;
        ItemStarted?.Invoke(this, item);

        var startTime = DateTime.Now;

        try
        {
            // Simulate render process - in real implementation this would call the audio exporter
            await RenderItemAsync(item, cancellationToken);

            item.ElapsedTime = DateTime.Now - startTime;
            item.Status = RenderStatus.Completed;
            ItemCompleted?.Invoke(this, item);
        }
        catch (OperationCanceledException)
        {
            item.Status = RenderStatus.Cancelled;
        }
        catch (Exception ex)
        {
            item.Status = RenderStatus.Failed;
            item.ErrorMessage = ex.Message;
            ItemFailed?.Invoke(this, item);
        }
    }

    private async Task RenderItemAsync(RenderQueueItem item, CancellationToken cancellationToken)
    {
        // This is a simplified implementation - the actual rendering would use the MusicEngine exporters
        var totalSamples = (long)(item.Duration * item.SampleRate);
        var processedSamples = 0L;
        var updateInterval = Math.Max(1, totalSamples / 100);

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(item.OutputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // In a real implementation, this would write to the actual file
        // For now, we simulate the rendering process
        while (processedSamples < totalSamples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            while (_isPaused && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }

            // Simulate processing a chunk
            var chunkSize = Math.Min(updateInterval, totalSamples - processedSamples);
            processedSamples += chunkSize;

            // Update progress
            item.Progress = (double)processedSamples / totalSamples * 100;

            // Estimate remaining time
            var elapsed = item.ElapsedTime.TotalSeconds;
            if (item.Progress > 0)
            {
                var totalEstimate = elapsed / (item.Progress / 100.0);
                item.EstimatedTime = TimeSpan.FromSeconds(totalEstimate - elapsed);
            }

            ProgressChanged?.Invoke(this, OverallProgress);

            // Small delay to prevent tight loop
            await Task.Delay(10, cancellationToken);
        }

        item.Progress = 100;

        // Estimate output file size
        item.OutputFileSize = EstimateFileSize(item);
    }

    private void EstimateItemRenderTime(RenderQueueItem item)
    {
        // Estimate based on duration and format
        // Real-time rendering would take approximately the duration time
        // Most systems can render faster than real-time
        var factor = item.Format switch
        {
            RenderFormat.Wav => 0.1,   // Very fast
            RenderFormat.Aiff => 0.1,  // Very fast
            RenderFormat.Flac => 0.3,  // Compression overhead
            RenderFormat.Mp3 => 0.5,   // Encoding overhead
            RenderFormat.Ogg => 0.5,   // Encoding overhead
            _ => 0.3
        };

        item.EstimatedTime = TimeSpan.FromSeconds(item.Duration * factor);
    }

    private static long EstimateFileSize(RenderQueueItem item)
    {
        var bytesPerSample = item.BitDepth / 8;
        var channels = 2; // Stereo
        var rawSize = (long)(item.Duration * item.SampleRate * bytesPerSample * channels);

        return item.Format switch
        {
            RenderFormat.Wav => rawSize + 44,  // WAV header
            RenderFormat.Aiff => rawSize + 54, // AIFF header
            RenderFormat.Flac => (long)(rawSize * 0.5), // ~50% compression
            RenderFormat.Mp3 => (long)(item.Duration * item.Mp3Bitrate * 1000 / 8), // Bitrate based
            RenderFormat.Ogg => (long)(item.Duration * 160 * 1000 / 8), // ~160kbps typical
            _ => rawSize
        };
    }
}
