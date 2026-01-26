using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for automatically saving projects at intervals.
/// Implements crash recovery through auto-save files.
/// </summary>
public sealed class AutoSaveService : IDisposable
{
    private static readonly Lazy<AutoSaveService> _instance = new(
        () => new AutoSaveService(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static AutoSaveService Instance => _instance.Value;

    private readonly System.Timers.Timer _autoSaveTimer;
    private readonly object _lock = new();
    private IProjectService? _projectService;
    private string _autoSaveDirectory;
    private bool _isEnabled = true;
    private int _intervalSeconds = 60; // Default 1 minute
    private int _maxAutoSaveFiles = 5;
    private bool _isDisposed;
    private bool _isSaving;

    /// <summary>
    /// Occurs when an auto-save operation completes successfully.
    /// </summary>
    public event EventHandler<AutoSaveEventArgs>? AutoSaveCompleted;

    /// <summary>
    /// Occurs when an auto-save operation fails.
    /// </summary>
    public event EventHandler<Exception>? AutoSaveFailed;

    /// <summary>
    /// Gets or sets whether auto-save is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            UpdateTimer();
        }
    }

    /// <summary>
    /// Gets or sets the auto-save interval in seconds. Minimum is 10 seconds.
    /// </summary>
    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set
        {
            _intervalSeconds = Math.Max(10, value);
            UpdateTimer();
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of auto-save files to keep per project.
    /// </summary>
    public int MaxAutoSaveFiles
    {
        get => _maxAutoSaveFiles;
        set => _maxAutoSaveFiles = Math.Max(1, value);
    }

    /// <summary>
    /// Gets the timestamp of the last successful auto-save.
    /// </summary>
    public DateTime? LastAutoSave { get; private set; }

    /// <summary>
    /// Gets the auto-save directory path.
    /// </summary>
    public string AutoSaveDirectory => _autoSaveDirectory;

    private static JsonSerializerOptions JsonOptions => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private AutoSaveService()
    {
        _autoSaveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicEngineEditor", "AutoSave");
        Directory.CreateDirectory(_autoSaveDirectory);

        _autoSaveTimer = new System.Timers.Timer(_intervalSeconds * 1000);
        _autoSaveTimer.Elapsed += OnAutoSaveTimerElapsed;
        _autoSaveTimer.AutoReset = true;
    }

    /// <summary>
    /// Initializes the auto-save service with a project service.
    /// </summary>
    /// <param name="projectService">The project service to use for saving.</param>
    public void Initialize(IProjectService projectService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        Start();
    }

    /// <summary>
    /// Starts the auto-save timer.
    /// </summary>
    public void Start()
    {
        if (_isEnabled && !_isDisposed)
        {
            _autoSaveTimer.Start();
        }
    }

    /// <summary>
    /// Stops the auto-save timer.
    /// </summary>
    public void Stop()
    {
        _autoSaveTimer.Stop();
    }

    /// <summary>
    /// Forces an immediate auto-save operation.
    /// </summary>
    public async Task SaveNowAsync()
    {
        if (_isDisposed || _isSaving)
            return;

        var project = _projectService?.CurrentProject;
        if (project == null)
            return;

        await PerformAutoSaveAsync(project);
    }

    /// <summary>
    /// Gets all available auto-save files, sorted by date (newest first).
    /// </summary>
    /// <returns>Array of auto-save file paths.</returns>
    public string[] GetAutoSaveFiles()
    {
        if (!Directory.Exists(_autoSaveDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(_autoSaveDirectory, "*.autosave")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
    }

    /// <summary>
    /// Gets auto-save files for a specific project.
    /// </summary>
    /// <param name="projectGuid">The project GUID.</param>
    /// <returns>Array of auto-save file paths for the project.</returns>
    public string[] GetAutoSaveFilesForProject(Guid projectGuid)
    {
        if (!Directory.Exists(_autoSaveDirectory))
            return Array.Empty<string>();

        var pattern = $"{projectGuid}_*.autosave";
        return Directory.GetFiles(_autoSaveDirectory, pattern)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
    }

    /// <summary>
    /// Gets information about all available auto-save files.
    /// </summary>
    /// <returns>List of auto-save file information.</returns>
    public List<AutoSaveInfo> GetAutoSaveInfos()
    {
        var infos = new List<AutoSaveInfo>();
        var files = GetAutoSaveFiles();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var manifest = JsonSerializer.Deserialize<AutoSaveManifest>(json, JsonOptions);

                if (manifest != null)
                {
                    infos.Add(new AutoSaveInfo
                    {
                        FilePath = file,
                        ProjectName = manifest.ProjectName,
                        ProjectGuid = Guid.TryParse(manifest.ProjectGuid, out var guid) ? guid : Guid.Empty,
                        SaveTime = DateTime.TryParse(manifest.SaveTime, out var time) ? time : File.GetLastWriteTime(file),
                        OriginalPath = manifest.OriginalPath,
                        FileSize = new FileInfo(file).Length
                    });
                }
            }
            catch
            {
                // Skip corrupted auto-save files
            }
        }

        return infos;
    }

    /// <summary>
    /// Recovers a project from an auto-save file.
    /// </summary>
    /// <param name="autoSaveFile">Path to the auto-save file.</param>
    /// <returns>The recovered project, or null if recovery failed.</returns>
    public async Task<MusicProject?> RecoverFromAutoSaveAsync(string autoSaveFile)
    {
        if (!File.Exists(autoSaveFile))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(autoSaveFile);
            var manifest = JsonSerializer.Deserialize<AutoSaveManifest>(json, JsonOptions);

            if (manifest == null)
                return null;

            var project = new MusicProject
            {
                Name = manifest.ProjectName,
                Guid = Guid.TryParse(manifest.ProjectGuid, out var guid) ? guid : Guid.NewGuid(),
                Namespace = manifest.Namespace ?? SanitizeNamespace(manifest.ProjectName),
                FilePath = manifest.OriginalPath,
                Created = DateTime.TryParse(manifest.Created, out var created) ? created : DateTime.UtcNow,
                Modified = DateTime.TryParse(manifest.SaveTime, out var modified) ? modified : DateTime.UtcNow,
                MusicEngineVersion = manifest.MusicEngineVersion ?? "1.0.0",
                Settings = manifest.Settings ?? new ProjectSettings(),
                IsDirty = true // Mark as dirty since it's recovered
            };

            // Restore scripts
            if (manifest.Scripts != null)
            {
                foreach (var scriptData in manifest.Scripts)
                {
                    var script = new MusicScript
                    {
                        FilePath = scriptData.FilePath,
                        Namespace = scriptData.Namespace,
                        IsEntryPoint = scriptData.IsEntryPoint,
                        Content = scriptData.Content,
                        Project = project
                    };
                    project.Scripts.Add(script);
                }
            }

            // Restore audio assets
            if (manifest.AudioAssets != null)
            {
                foreach (var assetData in manifest.AudioAssets)
                {
                    project.AudioAssets.Add(new AudioAsset
                    {
                        FilePath = assetData.FilePath,
                        Alias = assetData.Alias,
                        Category = assetData.Category
                    });
                }
            }

            // Restore references
            if (manifest.References != null)
            {
                foreach (var refData in manifest.References)
                {
                    project.References.Add(new ProjectReference
                    {
                        Type = refData.Type,
                        Path = refData.Path,
                        Alias = refData.Alias,
                        Version = refData.Version
                    });
                }
            }

            return project;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes a specific auto-save file.
    /// </summary>
    /// <param name="autoSaveFile">Path to the auto-save file to delete.</param>
    public void DeleteAutoSave(string autoSaveFile)
    {
        if (File.Exists(autoSaveFile))
        {
            try
            {
                File.Delete(autoSaveFile);
            }
            catch
            {
                // Ignore deletion errors
            }
        }
    }

    /// <summary>
    /// Cleans up old auto-save files for a project, keeping only the most recent ones.
    /// </summary>
    /// <param name="projectGuid">The project GUID.</param>
    public void CleanupAutoSaves(string projectGuid)
    {
        if (!Guid.TryParse(projectGuid, out var guid))
            return;

        CleanupAutoSaves(guid);
    }

    /// <summary>
    /// Cleans up old auto-save files for a project, keeping only the most recent ones.
    /// </summary>
    /// <param name="projectGuid">The project GUID.</param>
    public void CleanupAutoSaves(Guid projectGuid)
    {
        var files = GetAutoSaveFilesForProject(projectGuid);

        // Keep only the most recent files
        foreach (var file in files.Skip(_maxAutoSaveFiles))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore deletion errors
            }
        }
    }

    /// <summary>
    /// Cleans up all auto-save files older than the specified number of days.
    /// </summary>
    /// <param name="daysOld">Number of days after which auto-saves are considered old.</param>
    public void CleanupOldAutoSaves(int daysOld = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
        var files = GetAutoSaveFiles();

        foreach (var file in files)
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoffDate)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore deletion errors
            }
        }
    }

    /// <summary>
    /// Checks if there are any auto-save files available for recovery.
    /// </summary>
    /// <returns>True if auto-save files exist.</returns>
    public bool HasAutoSaves()
    {
        return GetAutoSaveFiles().Length > 0;
    }

    /// <summary>
    /// Checks if there are auto-save files newer than the project's last save time.
    /// </summary>
    /// <param name="project">The project to check.</param>
    /// <returns>True if newer auto-saves exist.</returns>
    public bool HasNewerAutoSaves(MusicProject project)
    {
        if (project == null)
            return false;

        var autoSaves = GetAutoSaveFilesForProject(project.Guid);
        if (autoSaves.Length == 0)
            return false;

        var newestAutoSave = File.GetLastWriteTimeUtc(autoSaves[0]);
        return newestAutoSave > project.Modified;
    }

    private void UpdateTimer()
    {
        lock (_lock)
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Interval = _intervalSeconds * 1000;

            if (_isEnabled && !_isDisposed)
            {
                _autoSaveTimer.Start();
            }
        }
    }

    private async void OnAutoSaveTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_isDisposed || _isSaving)
            return;

        var project = _projectService?.CurrentProject;
        if (project == null || !project.IsDirty)
            return;

        await PerformAutoSaveAsync(project);
    }

    private async Task PerformAutoSaveAsync(MusicProject project)
    {
        if (_isSaving)
            return;

        lock (_lock)
        {
            if (_isSaving)
                return;
            _isSaving = true;
        }

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{project.Guid}_{timestamp}.autosave";
            var filePath = Path.Combine(_autoSaveDirectory, fileName);

            var manifest = new AutoSaveManifest
            {
                Schema = "https://musicengine.dev/schema/autosave-1.0.json",
                ProjectName = project.Name,
                ProjectGuid = project.Guid.ToString(),
                Namespace = project.Namespace,
                OriginalPath = project.FilePath,
                Created = project.Created.ToString("o"),
                SaveTime = DateTime.UtcNow.ToString("o"),
                MusicEngineVersion = project.MusicEngineVersion,
                Settings = project.Settings,
                Scripts = project.Scripts.Select(s => new AutoSaveScript
                {
                    FilePath = s.FilePath,
                    Namespace = s.Namespace,
                    IsEntryPoint = s.IsEntryPoint,
                    Content = s.Content
                }).ToList(),
                AudioAssets = project.AudioAssets.Select(a => new AutoSaveAudioAsset
                {
                    FilePath = a.FilePath,
                    Alias = a.Alias,
                    Category = a.Category
                }).ToList(),
                References = project.References.Select(r => new AutoSaveReference
                {
                    Type = r.Type,
                    Path = r.Path,
                    Alias = r.Alias,
                    Version = r.Version
                }).ToList()
            };

            var json = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            LastAutoSave = DateTime.UtcNow;

            // Cleanup old auto-saves for this project
            CleanupAutoSaves(project.Guid);

            // Update recovery service session marker
            try
            {
                RecoveryService.Instance.UpdateSessionMarker();
            }
            catch
            {
                // Ignore recovery service errors during auto-save
            }

            AutoSaveCompleted?.Invoke(this, new AutoSaveEventArgs
            {
                FilePath = filePath,
                SaveTime = LastAutoSave.Value,
                ProjectName = project.Name
            });
        }
        catch (Exception ex)
        {
            AutoSaveFailed?.Invoke(this, ex);
        }
        finally
        {
            lock (_lock)
            {
                _isSaving = false;
            }
        }
    }

    private static string SanitizeNamespace(string name)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                result.Append(c);
            }
        }
        return result.Length > 0 ? result.ToString() : "MusicProject";
    }

    /// <summary>
    /// Disposes of the auto-save service and stops the timer.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _autoSaveTimer.Stop();
        _autoSaveTimer.Elapsed -= OnAutoSaveTimerElapsed;
        _autoSaveTimer.Dispose();
    }
}

/// <summary>
/// Event arguments for auto-save completion.
/// </summary>
public class AutoSaveEventArgs : EventArgs
{
    /// <summary>
    /// Gets the path to the auto-save file.
    /// </summary>
    public string FilePath { get; init; } = "";

    /// <summary>
    /// Gets the time the auto-save was performed.
    /// </summary>
    public DateTime SaveTime { get; init; }

    /// <summary>
    /// Gets the name of the project that was saved.
    /// </summary>
    public string ProjectName { get; init; } = "";
}

/// <summary>
/// Information about an auto-save file.
/// </summary>
public class AutoSaveInfo
{
    /// <summary>
    /// Gets the path to the auto-save file.
    /// </summary>
    public string FilePath { get; init; } = "";

    /// <summary>
    /// Gets the project name.
    /// </summary>
    public string ProjectName { get; init; } = "";

    /// <summary>
    /// Gets the project GUID.
    /// </summary>
    public Guid ProjectGuid { get; init; }

    /// <summary>
    /// Gets the time the auto-save was created.
    /// </summary>
    public DateTime SaveTime { get; init; }

    /// <summary>
    /// Gets the original project file path.
    /// </summary>
    public string OriginalPath { get; init; } = "";

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Gets a human-readable file size string.
    /// </summary>
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
}

/// <summary>
/// Internal manifest structure for auto-save files.
/// </summary>
internal class AutoSaveManifest
{
    public string Schema { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string ProjectGuid { get; set; } = "";
    public string? Namespace { get; set; }
    public string OriginalPath { get; set; } = "";
    public string Created { get; set; } = "";
    public string SaveTime { get; set; } = "";
    public string? MusicEngineVersion { get; set; }
    public ProjectSettings? Settings { get; set; }
    public List<AutoSaveScript>? Scripts { get; set; }
    public List<AutoSaveAudioAsset>? AudioAssets { get; set; }
    public List<AutoSaveReference>? References { get; set; }
}

internal class AutoSaveScript
{
    public string FilePath { get; set; } = "";
    public string Namespace { get; set; } = "";
    public bool IsEntryPoint { get; set; }
    public string Content { get; set; } = "";
}

internal class AutoSaveAudioAsset
{
    public string FilePath { get; set; } = "";
    public string Alias { get; set; } = "";
    public string Category { get; set; } = "";
}

internal class AutoSaveReference
{
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public string Alias { get; set; } = "";
    public string? Version { get; set; }
}
