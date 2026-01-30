// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Information about a recoverable session from a previous crash.
/// </summary>
public class RecoveryInfo
{
    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the auto-save file.
    /// </summary>
    public string AutoSavePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time when the crash/abnormal exit occurred.
    /// </summary>
    public DateTime CrashTime { get; set; }

    /// <summary>
    /// Gets or sets the original project file path.
    /// </summary>
    public string OriginalProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size of the auto-save in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets the project GUID.
    /// </summary>
    public Guid ProjectGuid { get; set; }

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

    /// <summary>
    /// Gets the time ago string relative to now.
    /// </summary>
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.Now - CrashTime;

            if (diff.TotalMinutes < 1)
                return "Just now";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours} hours ago";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays} days ago";
            if (diff.TotalDays < 30)
                return $"{(int)(diff.TotalDays / 7)} weeks ago";

            return CrashTime.ToString("MMM d");
        }
    }
}

/// <summary>
/// Internal structure for the recovery marker file.
/// </summary>
internal class RecoveryMarker
{
    public string SessionId { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public Guid ProjectGuid { get; set; }
    public DateTime SessionStarted { get; set; }
    public DateTime LastUpdate { get; set; }
    public int ProcessId { get; set; }
}

/// <summary>
/// Service for crash recovery functionality.
/// Tracks active sessions and provides recovery from auto-save files after abnormal exits.
/// </summary>
public class RecoveryService : IDisposable
{
    private const string RecoveryMarkerFileName = "recovery.marker";
    private const string SessionLockFileName = "session.lock";

    private static readonly Lazy<RecoveryService> _instance = new(
        () => new RecoveryService(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the RecoveryService.
    /// </summary>
    public static RecoveryService Instance => _instance.Value;

    private readonly string _recoveryDirectory;
    private readonly string _markerFilePath;
    private readonly string _lockFilePath;
    private readonly object _lock = new();
    private FileStream? _lockFileStream;
    private bool _isDisposed;
    private string _currentSessionId = string.Empty;

    private static JsonSerializerOptions JsonOptions => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Occurs when a session is marked as active.
    /// </summary>
    public event EventHandler<string>? SessionMarked;

    /// <summary>
    /// Occurs when a session is marked as closed.
    /// </summary>
    public event EventHandler? SessionClosed;

    /// <summary>
    /// Occurs when a recovery point is found.
    /// </summary>
    public event EventHandler<RecoveryInfo>? RecoveryPointFound;

    /// <summary>
    /// Gets the recovery directory path.
    /// </summary>
    public string RecoveryDirectory => _recoveryDirectory;

    /// <summary>
    /// Gets whether a session is currently active.
    /// </summary>
    public bool IsSessionActive => !string.IsNullOrEmpty(_currentSessionId) && _lockFileStream != null;

    private RecoveryService()
    {
        _recoveryDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicEngineEditor", "Recovery");
        Directory.CreateDirectory(_recoveryDirectory);

        _markerFilePath = Path.Combine(_recoveryDirectory, RecoveryMarkerFileName);
        _lockFilePath = Path.Combine(_recoveryDirectory, SessionLockFileName);
    }

    /// <summary>
    /// Checks if there are any recoverable sessions from a previous crash.
    /// A crash is detected when a marker file exists but no lock file is held.
    /// </summary>
    /// <returns>True if there are recoverable sessions available.</returns>
    public bool HasRecoverableSession()
    {
        if (!File.Exists(_markerFilePath))
            return false;

        // Check if the lock file is held by another process
        if (IsLockFileHeld())
            return false;

        // Marker exists but no lock - indicates crash
        try
        {
            var json = File.ReadAllText(_markerFilePath);
            var marker = JsonSerializer.Deserialize<RecoveryMarker>(json, JsonOptions);

            if (marker == null)
                return false;

            // Check if the process is still running (might be a different instance)
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(marker.ProcessId);
                if (!process.HasExited)
                    return false; // Process is still running, not a crash
            }
            catch (ArgumentException)
            {
                // Process not found - definitely a crash
            }
            catch (InvalidOperationException)
            {
                // Process info not available - assume crash
            }

            // Check if there are corresponding auto-save files
            var autoSaveService = AutoSaveService.Instance;
            var autoSaves = autoSaveService.GetAutoSaveFilesForProject(marker.ProjectGuid);

            return autoSaves.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all available recovery points from previous sessions.
    /// </summary>
    /// <returns>List of recovery information for each recoverable project.</returns>
    public List<RecoveryInfo> GetRecoveryPoints()
    {
        var recoveryPoints = new List<RecoveryInfo>();

        // Check marker file for crash information
        if (File.Exists(_markerFilePath) && !IsLockFileHeld())
        {
            try
            {
                var json = File.ReadAllText(_markerFilePath);
                var marker = JsonSerializer.Deserialize<RecoveryMarker>(json, JsonOptions);

                if (marker != null)
                {
                    // Verify process is not running
                    bool processRunning = false;
                    try
                    {
                        var process = System.Diagnostics.Process.GetProcessById(marker.ProcessId);
                        processRunning = !process.HasExited;
                    }
                    catch { }

                    if (!processRunning)
                    {
                        // Get auto-save files for this project
                        var autoSaveService = AutoSaveService.Instance;
                        var autoSaves = autoSaveService.GetAutoSaveFilesForProject(marker.ProjectGuid);

                        if (autoSaves.Length > 0)
                        {
                            var latestAutoSave = autoSaves[0]; // Already sorted by date descending
                            var fileInfo = new FileInfo(latestAutoSave);

                            var info = new RecoveryInfo
                            {
                                ProjectName = marker.ProjectName,
                                AutoSavePath = latestAutoSave,
                                CrashTime = marker.LastUpdate,
                                OriginalProjectPath = marker.ProjectPath,
                                FileSize = fileInfo.Length,
                                ProjectGuid = marker.ProjectGuid
                            };

                            recoveryPoints.Add(info);
                            RecoveryPointFound?.Invoke(this, info);
                        }
                    }
                }
            }
            catch
            {
                // Ignore marker parsing errors
            }
        }

        // Also check for orphaned auto-save files (from older crashes where marker was lost)
        var allAutoSaves = AutoSaveService.Instance.GetAutoSaveInfos();
        var existingGuids = new HashSet<Guid>(recoveryPoints.Select(r => r.ProjectGuid));

        foreach (var autoSave in allAutoSaves)
        {
            // Skip if already found via marker
            if (existingGuids.Contains(autoSave.ProjectGuid))
                continue;

            // Check if this auto-save is from a crashed session (older than a threshold)
            var age = DateTime.Now - autoSave.SaveTime;
            if (age.TotalMinutes > 5) // Only consider auto-saves older than 5 minutes as crash candidates
            {
                var info = new RecoveryInfo
                {
                    ProjectName = autoSave.ProjectName,
                    AutoSavePath = autoSave.FilePath,
                    CrashTime = autoSave.SaveTime,
                    OriginalProjectPath = autoSave.OriginalPath,
                    FileSize = autoSave.FileSize,
                    ProjectGuid = autoSave.ProjectGuid
                };

                // Only add if we don't already have this project
                if (!existingGuids.Contains(autoSave.ProjectGuid))
                {
                    recoveryPoints.Add(info);
                    existingGuids.Add(autoSave.ProjectGuid);
                }
            }
        }

        return recoveryPoints.OrderByDescending(r => r.CrashTime).ToList();
    }

    /// <summary>
    /// Marks the current session as active by creating a marker file and acquiring a lock.
    /// This should be called when a project is opened.
    /// </summary>
    /// <param name="projectPath">The path to the project file.</param>
    /// <param name="projectName">The name of the project.</param>
    /// <param name="projectGuid">The GUID of the project.</param>
    public void MarkSessionActive(string projectPath, string? projectName = null, Guid? projectGuid = null)
    {
        lock (_lock)
        {
            if (_isDisposed)
                return;

            try
            {
                // Generate session ID
                _currentSessionId = Guid.NewGuid().ToString("N");

                // Create marker file
                var marker = new RecoveryMarker
                {
                    SessionId = _currentSessionId,
                    ProjectPath = projectPath,
                    ProjectName = projectName ?? Path.GetFileNameWithoutExtension(projectPath),
                    ProjectGuid = projectGuid ?? Guid.NewGuid(),
                    SessionStarted = DateTime.UtcNow,
                    LastUpdate = DateTime.UtcNow,
                    ProcessId = Environment.ProcessId
                };

                var json = JsonSerializer.Serialize(marker, JsonOptions);
                File.WriteAllText(_markerFilePath, json);

                // Acquire lock file
                AcquireLockFile();

                SessionMarked?.Invoke(this, _currentSessionId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to mark session active: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Marks the current session as active using a MusicProject.
    /// </summary>
    /// <param name="project">The music project.</param>
    public void MarkSessionActive(MusicProject project)
    {
        if (project == null)
            return;

        MarkSessionActive(project.FilePath, project.Name, project.Guid);
    }

    /// <summary>
    /// Updates the session marker with the latest timestamp.
    /// This should be called periodically to indicate the session is still alive.
    /// </summary>
    public void UpdateSessionMarker()
    {
        lock (_lock)
        {
            if (_isDisposed || string.IsNullOrEmpty(_currentSessionId))
                return;

            try
            {
                if (File.Exists(_markerFilePath))
                {
                    var json = File.ReadAllText(_markerFilePath);
                    var marker = JsonSerializer.Deserialize<RecoveryMarker>(json, JsonOptions);

                    if (marker != null && marker.SessionId == _currentSessionId)
                    {
                        marker.LastUpdate = DateTime.UtcNow;
                        json = JsonSerializer.Serialize(marker, JsonOptions);
                        File.WriteAllText(_markerFilePath, json);
                    }
                }
            }
            catch
            {
                // Ignore update errors
            }
        }
    }

    /// <summary>
    /// Marks the current session as cleanly closed.
    /// This removes the marker file and releases the lock.
    /// </summary>
    public void MarkSessionClosed()
    {
        lock (_lock)
        {
            try
            {
                // Release lock file first
                ReleaseLockFile();

                // Delete marker file
                if (File.Exists(_markerFilePath))
                {
                    File.Delete(_markerFilePath);
                }

                _currentSessionId = string.Empty;

                SessionClosed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to mark session closed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Recovers a project from the specified recovery point.
    /// </summary>
    /// <param name="info">The recovery information.</param>
    /// <returns>The recovered project, or null if recovery failed.</returns>
    public async Task<MusicProject?> RecoverAsync(RecoveryInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.AutoSavePath))
            return null;

        var autoSaveService = AutoSaveService.Instance;
        return await autoSaveService.RecoverFromAutoSaveAsync(info.AutoSavePath);
    }

    /// <summary>
    /// Deletes a recovery point and its associated auto-save file.
    /// </summary>
    /// <param name="info">The recovery information to delete.</param>
    public void DeleteRecoveryPoint(RecoveryInfo info)
    {
        if (info == null)
            return;

        try
        {
            // Delete auto-save file
            if (File.Exists(info.AutoSavePath))
            {
                AutoSaveService.Instance.DeleteAutoSave(info.AutoSavePath);
            }

            // If this was the project in the marker file, clean up marker too
            if (File.Exists(_markerFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_markerFilePath);
                    var marker = JsonSerializer.Deserialize<RecoveryMarker>(json, JsonOptions);

                    if (marker != null && marker.ProjectGuid == info.ProjectGuid)
                    {
                        File.Delete(_markerFilePath);
                    }
                }
                catch
                {
                    // Ignore marker cleanup errors
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete recovery point: {ex.Message}");
        }
    }

    /// <summary>
    /// Cleans up old recovery files and markers.
    /// </summary>
    /// <param name="daysToKeep">Number of days to keep recovery files.</param>
    public void CleanupOldRecoveries(int daysToKeep = 7)
    {
        try
        {
            // Clean up old auto-save files
            AutoSaveService.Instance.CleanupOldAutoSaves(daysToKeep);

            // Clean up stale marker file if it exists and lock is not held
            if (File.Exists(_markerFilePath) && !IsLockFileHeld())
            {
                var json = File.ReadAllText(_markerFilePath);
                var marker = JsonSerializer.Deserialize<RecoveryMarker>(json, JsonOptions);

                if (marker != null)
                {
                    var age = DateTime.UtcNow - marker.LastUpdate;
                    if (age.TotalDays > daysToKeep)
                    {
                        File.Delete(_markerFilePath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cleanup old recoveries: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all recovery data for a clean start.
    /// </summary>
    public void ClearAllRecoveryData()
    {
        lock (_lock)
        {
            try
            {
                // Delete marker file
                if (File.Exists(_markerFilePath))
                {
                    File.Delete(_markerFilePath);
                }

                // Delete all auto-save files
                var autoSaves = AutoSaveService.Instance.GetAutoSaveFiles();
                foreach (var file in autoSaves)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Continue with other files
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear recovery data: {ex.Message}");
            }
        }
    }

    private bool IsLockFileHeld()
    {
        if (!File.Exists(_lockFilePath))
            return false;

        try
        {
            // Try to open the lock file exclusively
            using var stream = new FileStream(_lockFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false; // We could open it, so it's not held
        }
        catch (IOException)
        {
            return true; // File is held by another process
        }
        catch
        {
            return false;
        }
    }

    private void AcquireLockFile()
    {
        try
        {
            ReleaseLockFile(); // Release any existing lock

            // Create/open lock file with exclusive access
            _lockFileStream = new FileStream(
                _lockFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.DeleteOnClose);

            // Write session info to lock file
            using var writer = new StreamWriter(_lockFileStream, leaveOpen: true);
            writer.WriteLine(_currentSessionId);
            writer.WriteLine(Environment.ProcessId);
            writer.WriteLine(DateTime.UtcNow.ToString("o"));
            writer.Flush();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to acquire lock file: {ex.Message}");
        }
    }

    private void ReleaseLockFile()
    {
        try
        {
            _lockFileStream?.Dispose();
            _lockFileStream = null;

            // Lock file should be auto-deleted due to DeleteOnClose, but try anyway
            if (File.Exists(_lockFilePath))
            {
                try
                {
                    File.Delete(_lockFilePath);
                }
                catch
                {
                    // Ignore - file might still be locked
                }
            }
        }
        catch
        {
            // Ignore release errors
        }
    }

    /// <summary>
    /// Disposes of the recovery service and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        // Clean shutdown - mark session as closed
        MarkSessionClosed();
    }
}
