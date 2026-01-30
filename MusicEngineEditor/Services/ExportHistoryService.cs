// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for tracking and managing export history.
/// Logs all exports with their settings for easy re-export.
/// </summary>
public sealed class ExportHistoryService : INotifyPropertyChanged
{
    private static readonly Lazy<ExportHistoryService> _instance = new(
        () => new ExportHistoryService(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    public static ExportHistoryService Instance => _instance.Value;

    private readonly List<ExportHistoryEntry> _exportHistory = new();
    private readonly string _historyFilePath;
    private const int MaxHistoryEntries = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<ExportHistoryEntry>? ExportLogged;
    public event EventHandler<ExportHistoryEntry>? ExportDeleted;
    public event EventHandler? HistoryCleared;

    private ExportHistoryService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "MusicEngineEditor");
        Directory.CreateDirectory(appFolder);
        _historyFilePath = Path.Combine(appFolder, "export_history.json");

        LoadHistory();
    }

    #region Properties

    /// <summary>
    /// Gets all export history entries.
    /// </summary>
    public IReadOnlyList<ExportHistoryEntry> History => _exportHistory.AsReadOnly();

    /// <summary>
    /// Gets the count of export history entries.
    /// </summary>
    public int HistoryCount => _exportHistory.Count;

    /// <summary>
    /// Gets whether there are any history entries.
    /// </summary>
    public bool HasHistory => _exportHistory.Count > 0;

    /// <summary>
    /// Gets the most recent export entry.
    /// </summary>
    public ExportHistoryEntry? MostRecentExport => _exportHistory.FirstOrDefault();

    #endregion

    #region Public Methods

    /// <summary>
    /// Logs a new export operation.
    /// </summary>
    /// <param name="projectName">Name of the exported project.</param>
    /// <param name="outputPath">Path where the file was exported.</param>
    /// <param name="format">Export format (WAV, MP3, FLAC, etc.).</param>
    /// <param name="settings">The export settings used.</param>
    /// <returns>The created history entry.</returns>
    public ExportHistoryEntry LogExport(string projectName, string outputPath, string format, ExportSettings settings)
    {
        var entry = new ExportHistoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            ProjectName = projectName,
            OutputPath = outputPath,
            Format = format,
            Settings = settings,
            ExportedAt = DateTime.UtcNow,
            FileSize = GetFileSize(outputPath),
            Duration = settings.Duration,
            Success = true
        };

        _exportHistory.Insert(0, entry);

        // Trim history if needed
        while (_exportHistory.Count > MaxHistoryEntries)
        {
            _exportHistory.RemoveAt(_exportHistory.Count - 1);
        }

        SaveHistory();

        ExportLogged?.Invoke(this, entry);
        NotifyPropertyChanged(nameof(History));
        NotifyPropertyChanged(nameof(HistoryCount));
        NotifyPropertyChanged(nameof(HasHistory));
        NotifyPropertyChanged(nameof(MostRecentExport));

        return entry;
    }

    /// <summary>
    /// Logs a failed export operation.
    /// </summary>
    public ExportHistoryEntry LogFailedExport(string projectName, string outputPath, string format, ExportSettings settings, string errorMessage)
    {
        var entry = new ExportHistoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            ProjectName = projectName,
            OutputPath = outputPath,
            Format = format,
            Settings = settings,
            ExportedAt = DateTime.UtcNow,
            Success = false,
            ErrorMessage = errorMessage
        };

        _exportHistory.Insert(0, entry);

        // Trim history if needed
        while (_exportHistory.Count > MaxHistoryEntries)
        {
            _exportHistory.RemoveAt(_exportHistory.Count - 1);
        }

        SaveHistory();

        ExportLogged?.Invoke(this, entry);
        NotifyPropertyChanged(nameof(History));
        NotifyPropertyChanged(nameof(HistoryCount));
        NotifyPropertyChanged(nameof(HasHistory));

        return entry;
    }

    /// <summary>
    /// Gets history entries for a specific project.
    /// </summary>
    public IReadOnlyList<ExportHistoryEntry> GetProjectHistory(string projectName)
    {
        return _exportHistory
            .Where(e => e.ProjectName == projectName)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets history entries by format.
    /// </summary>
    public IReadOnlyList<ExportHistoryEntry> GetHistoryByFormat(string format)
    {
        return _exportHistory
            .Where(e => e.Format.Equals(format, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets successful exports only.
    /// </summary>
    public IReadOnlyList<ExportHistoryEntry> GetSuccessfulExports()
    {
        return _exportHistory
            .Where(e => e.Success)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets failed exports only.
    /// </summary>
    public IReadOnlyList<ExportHistoryEntry> GetFailedExports()
    {
        return _exportHistory
            .Where(e => !e.Success)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets a specific history entry by ID.
    /// </summary>
    public ExportHistoryEntry? GetEntry(string id)
    {
        return _exportHistory.FirstOrDefault(e => e.Id == id);
    }

    /// <summary>
    /// Deletes a history entry.
    /// </summary>
    public bool DeleteEntry(string id)
    {
        var entry = _exportHistory.FirstOrDefault(e => e.Id == id);
        if (entry == null)
            return false;

        _exportHistory.Remove(entry);
        SaveHistory();

        ExportDeleted?.Invoke(this, entry);
        NotifyPropertyChanged(nameof(History));
        NotifyPropertyChanged(nameof(HistoryCount));
        NotifyPropertyChanged(nameof(HasHistory));
        NotifyPropertyChanged(nameof(MostRecentExport));

        return true;
    }

    /// <summary>
    /// Clears all export history.
    /// </summary>
    public void ClearHistory()
    {
        _exportHistory.Clear();
        SaveHistory();

        HistoryCleared?.Invoke(this, EventArgs.Empty);
        NotifyPropertyChanged(nameof(History));
        NotifyPropertyChanged(nameof(HistoryCount));
        NotifyPropertyChanged(nameof(HasHistory));
        NotifyPropertyChanged(nameof(MostRecentExport));
    }

    /// <summary>
    /// Checks if the exported file still exists.
    /// </summary>
    public bool FileExists(ExportHistoryEntry entry)
    {
        return File.Exists(entry.OutputPath);
    }

    /// <summary>
    /// Opens the folder containing the exported file.
    /// </summary>
    public void OpenExportFolder(ExportHistoryEntry entry)
    {
        var folder = Path.GetDirectoryName(entry.OutputPath);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            System.Diagnostics.Process.Start("explorer.exe", folder);
        }
    }

    /// <summary>
    /// Gets export statistics.
    /// </summary>
    public ExportStatistics GetStatistics()
    {
        var stats = new ExportStatistics
        {
            TotalExports = _exportHistory.Count,
            SuccessfulExports = _exportHistory.Count(e => e.Success),
            FailedExports = _exportHistory.Count(e => !e.Success),
            TotalFileSize = _exportHistory.Where(e => e.Success).Sum(e => e.FileSize),
            TotalDuration = TimeSpan.FromSeconds(_exportHistory.Where(e => e.Success).Sum(e => e.Duration.TotalSeconds)),
            FormatBreakdown = _exportHistory
                .GroupBy(e => e.Format)
                .ToDictionary(g => g.Key, g => g.Count()),
            FirstExport = _exportHistory.LastOrDefault()?.ExportedAt,
            LastExport = _exportHistory.FirstOrDefault()?.ExportedAt
        };

        return stats;
    }

    /// <summary>
    /// Exports history to a file.
    /// </summary>
    public async Task ExportHistoryToFileAsync(string filePath)
    {
        var json = JsonSerializer.Serialize(_exportHistory, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    #endregion

    #region Private Methods

    private void LoadHistory()
    {
        if (!File.Exists(_historyFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_historyFilePath);
            var entries = JsonSerializer.Deserialize<List<ExportHistoryEntry>>(json, JsonOptions);

            if (entries != null)
            {
                _exportHistory.Clear();
                _exportHistory.AddRange(entries.Take(MaxHistoryEntries));
            }
        }
        catch
        {
            // Ignore load errors
        }
    }

    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_exportHistory, JsonOptions);
            File.WriteAllText(_historyFilePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private static long GetFileSize(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }
        }
        catch
        {
            // Ignore errors
        }
        return 0;
    }

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

#region Models

/// <summary>
/// Represents a single export history entry.
/// </summary>
public class ExportHistoryEntry : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _projectName = string.Empty;
    private string _outputPath = string.Empty;
    private string _format = string.Empty;
    private DateTime _exportedAt;
    private long _fileSize;
    private TimeSpan _duration;
    private bool _success;
    private string? _errorMessage;
    private ExportSettings? _settings;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string ProjectName { get => _projectName; set { _projectName = value; OnPropertyChanged(); } }
    public string OutputPath { get => _outputPath; set { _outputPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileName)); } }
    public string Format { get => _format; set { _format = value; OnPropertyChanged(); } }
    public DateTime ExportedAt { get => _exportedAt; set { _exportedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExportedAtDisplay)); } }
    public long FileSize { get => _fileSize; set { _fileSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeDisplay)); } }
    public TimeSpan Duration { get => _duration; set { _duration = value; OnPropertyChanged(); OnPropertyChanged(nameof(DurationDisplay)); } }
    public bool Success { get => _success; set { _success = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); } }
    public string? ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); } }
    public ExportSettings? Settings { get => _settings; set { _settings = value; OnPropertyChanged(); } }

    public string FileName => Path.GetFileName(OutputPath);

    public string ExportedAtDisplay => ExportedAt.ToLocalTime().ToString("g");

    public string FileSizeDisplay
    {
        get
        {
            if (FileSize < 1024)
                return $"{FileSize} B";
            if (FileSize < 1024 * 1024)
                return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }

    public string DurationDisplay => Duration.TotalHours >= 1
        ? $"{(int)Duration.TotalHours}:{Duration.Minutes:D2}:{Duration.Seconds:D2}"
        : $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}";

    public string StatusDisplay => Success ? "Success" : "Failed";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Export settings stored with each history entry.
/// </summary>
public class ExportSettings
{
    public int SampleRate { get; set; } = 44100;
    public int BitDepth { get; set; } = 16;
    public int Channels { get; set; } = 2;
    public int? Bitrate { get; set; }
    public string? Quality { get; set; }
    public bool Normalize { get; set; }
    public double NormalizeLevel { get; set; } = -1.0;
    public bool Dither { get; set; }
    public string? DitherType { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public bool ExportMarkers { get; set; }
    public bool SplitByMarkers { get; set; }
    public string? MetadataTitle { get; set; }
    public string? MetadataArtist { get; set; }
    public string? MetadataAlbum { get; set; }
    public string? MetadataGenre { get; set; }
    public int? MetadataYear { get; set; }
    public Dictionary<string, string>? CustomMetadata { get; set; }

    public string SettingsDisplay
    {
        get
        {
            var parts = new List<string>
            {
                $"{SampleRate / 1000.0:F1}kHz",
                $"{BitDepth}bit",
                Channels == 1 ? "Mono" : "Stereo"
            };

            if (Bitrate.HasValue)
            {
                parts.Add($"{Bitrate}kbps");
            }

            if (Normalize)
            {
                parts.Add($"Normalized to {NormalizeLevel:F1}dB");
            }

            return string.Join(", ", parts);
        }
    }
}

/// <summary>
/// Export statistics.
/// </summary>
public class ExportStatistics
{
    public int TotalExports { get; set; }
    public int SuccessfulExports { get; set; }
    public int FailedExports { get; set; }
    public long TotalFileSize { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public Dictionary<string, int> FormatBreakdown { get; set; } = new();
    public DateTime? FirstExport { get; set; }
    public DateTime? LastExport { get; set; }

    public double SuccessRate => TotalExports > 0 ? (double)SuccessfulExports / TotalExports * 100 : 0;

    public string TotalFileSizeDisplay
    {
        get
        {
            if (TotalFileSize < 1024)
                return $"{TotalFileSize} B";
            if (TotalFileSize < 1024 * 1024)
                return $"{TotalFileSize / 1024.0:F1} KB";
            if (TotalFileSize < 1024 * 1024 * 1024)
                return $"{TotalFileSize / (1024.0 * 1024.0):F1} MB";
            return $"{TotalFileSize / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}

#endregion
