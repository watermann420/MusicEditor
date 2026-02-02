// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service for managing recent projects with persistence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents a recent project entry with metadata.
/// </summary>
public class RecentProject
{
    /// <summary>
    /// Gets or sets the full path to the project file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last opened date.
    /// </summary>
    public DateTime LastOpened { get; set; }

    /// <summary>
    /// Gets or sets whether this project is pinned/favorited.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Gets the file name without path.
    /// </summary>
    [JsonIgnore]
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the directory containing the project.
    /// </summary>
    [JsonIgnore]
    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;

    /// <summary>
    /// Gets a relative time string for the last opened date.
    /// </summary>
    [JsonIgnore]
    public string LastOpenedRelative
    {
        get
        {
            var diff = DateTime.Now - LastOpened;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)}w ago";
            return LastOpened.ToString("MMM d, yyyy");
        }
    }

    /// <summary>
    /// Gets the last modified date of the file (if it exists).
    /// </summary>
    [JsonIgnore]
    public DateTime? LastModified
    {
        get
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return File.GetLastWriteTime(FilePath);
                }
            }
            catch
            {
                // Ignore file access errors
            }
            return null;
        }
    }

    /// <summary>
    /// Gets a formatted last modified date string.
    /// </summary>
    [JsonIgnore]
    public string LastModifiedFormatted
    {
        get
        {
            var modified = LastModified;
            if (modified == null) return "Unknown";

            var diff = DateTime.Now - modified.Value;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)}w ago";
            return modified.Value.ToString("MMM d, yyyy");
        }
    }

    /// <summary>
    /// Gets whether the project file exists.
    /// </summary>
    [JsonIgnore]
    public bool Exists => File.Exists(FilePath);
}

/// <summary>
/// Service for managing recently opened projects with JSON persistence.
/// </summary>
public class RecentProjectsService : IRecentProjectsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor");

    private static readonly string RecentProjectsFilePath = Path.Combine(SettingsFolder, "recent-projects.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const int MaxRecentProjects = 10;

    private List<RecentProject> _recentProjects = new();
    private bool _showWelcomeOnStartup = true;

    /// <summary>
    /// Gets or sets whether to show the welcome screen on startup.
    /// </summary>
    public bool ShowWelcomeOnStartup
    {
        get => _showWelcomeOnStartup;
        set => _showWelcomeOnStartup = value;
    }

    /// <summary>
    /// Gets the list of recent projects.
    /// </summary>
    public IReadOnlyList<RecentProject> RecentProjects => _recentProjects.AsReadOnly();

    /// <summary>
    /// Loads recent projects from the settings file.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(RecentProjectsFilePath))
            {
                var json = await File.ReadAllTextAsync(RecentProjectsFilePath);
                var data = JsonSerializer.Deserialize<RecentProjectsData>(json, JsonOptions);
                if (data != null)
                {
                    _recentProjects = data.Projects ?? new List<RecentProject>();
                    _showWelcomeOnStartup = data.ShowWelcomeOnStartup;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load recent projects: {ex.Message}");
            _recentProjects = new List<RecentProject>();
        }

        // Remove projects that no longer exist
        _recentProjects.RemoveAll(p => !p.Exists);
    }

    /// <summary>
    /// Saves recent projects to the settings file.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);

            var data = new RecentProjectsData
            {
                Projects = _recentProjects,
                ShowWelcomeOnStartup = _showWelcomeOnStartup
            };

            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(RecentProjectsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save recent projects: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds or updates a project in the recent list.
    /// </summary>
    /// <param name="filePath">The full path to the project file.</param>
    /// <param name="name">The project name (optional, derived from file if not provided).</param>
    public async Task AddProjectAsync(string filePath, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        // Normalize path
        filePath = Path.GetFullPath(filePath);

        // Find existing entry or create new
        var existing = _recentProjects.FirstOrDefault(p =>
            string.Equals(p.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            // Update existing entry
            existing.LastOpened = DateTime.Now;
            if (!string.IsNullOrEmpty(name))
            {
                existing.Name = name;
            }

            // Move to top (but keep pinned items above)
            _recentProjects.Remove(existing);
            InsertInOrder(existing);
        }
        else
        {
            // Create new entry
            var entry = new RecentProject
            {
                FilePath = filePath,
                Name = name ?? Path.GetFileNameWithoutExtension(filePath),
                LastOpened = DateTime.Now,
                IsPinned = false
            };

            InsertInOrder(entry);

            // Trim to max count
            while (_recentProjects.Count > MaxRecentProjects)
            {
                // Remove the oldest non-pinned project
                var toRemove = _recentProjects.LastOrDefault(p => !p.IsPinned);
                if (toRemove != null)
                {
                    _recentProjects.Remove(toRemove);
                }
                else
                {
                    // All are pinned, remove the oldest anyway
                    _recentProjects.RemoveAt(_recentProjects.Count - 1);
                }
            }
        }

        await SaveAsync();
    }

    /// <summary>
    /// Removes a project from the recent list.
    /// </summary>
    /// <param name="filePath">The full path to the project file.</param>
    public async Task RemoveProjectAsync(string filePath)
    {
        filePath = Path.GetFullPath(filePath);
        var entry = _recentProjects.FirstOrDefault(p =>
            string.Equals(p.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (entry != null)
        {
            _recentProjects.Remove(entry);
            await SaveAsync();
        }
    }

    /// <summary>
    /// Toggles the pinned state of a project.
    /// </summary>
    /// <param name="filePath">The full path to the project file.</param>
    public async Task TogglePinnedAsync(string filePath)
    {
        filePath = Path.GetFullPath(filePath);
        var entry = _recentProjects.FirstOrDefault(p =>
            string.Equals(p.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (entry != null)
        {
            entry.IsPinned = !entry.IsPinned;

            // Re-order the list
            _recentProjects.Remove(entry);
            InsertInOrder(entry);

            await SaveAsync();
        }
    }

    /// <summary>
    /// Sets whether to show the welcome screen on startup.
    /// </summary>
    /// <param name="show">True to show, false to hide.</param>
    public async Task SetShowWelcomeOnStartupAsync(bool show)
    {
        _showWelcomeOnStartup = show;
        await SaveAsync();
    }

    /// <summary>
    /// Clears all recent projects.
    /// </summary>
    public async Task ClearAllAsync()
    {
        _recentProjects.Clear();
        await SaveAsync();
    }

    /// <summary>
    /// Inserts an entry in the correct order (pinned first, then by last opened).
    /// </summary>
    private void InsertInOrder(RecentProject entry)
    {
        if (entry.IsPinned)
        {
            // Find the position among pinned items (by last opened)
            var insertIndex = 0;
            for (int i = 0; i < _recentProjects.Count; i++)
            {
                if (!_recentProjects[i].IsPinned)
                {
                    insertIndex = i;
                    break;
                }
                if (_recentProjects[i].LastOpened < entry.LastOpened)
                {
                    insertIndex = i;
                    break;
                }
                insertIndex = i + 1;
            }
            _recentProjects.Insert(insertIndex, entry);
        }
        else
        {
            // Find the position among non-pinned items (by last opened)
            var firstNonPinnedIndex = _recentProjects.FindIndex(p => !p.IsPinned);
            if (firstNonPinnedIndex < 0)
            {
                // All items are pinned, add at the end
                _recentProjects.Add(entry);
            }
            else
            {
                var insertIndex = firstNonPinnedIndex;
                for (int i = firstNonPinnedIndex; i < _recentProjects.Count; i++)
                {
                    if (_recentProjects[i].LastOpened < entry.LastOpened)
                    {
                        insertIndex = i;
                        break;
                    }
                    insertIndex = i + 1;
                }
                _recentProjects.Insert(insertIndex, entry);
            }
        }
    }

    /// <summary>
    /// Data container for JSON serialization.
    /// </summary>
    private class RecentProjectsData
    {
        public List<RecentProject> Projects { get; set; } = new();
        public bool ShowWelcomeOnStartup { get; set; } = true;
    }
}

/// <summary>
/// Interface for the recent projects service.
/// </summary>
public interface IRecentProjectsService
{
    /// <summary>
    /// Gets or sets whether to show the welcome screen on startup.
    /// </summary>
    bool ShowWelcomeOnStartup { get; set; }

    /// <summary>
    /// Gets the list of recent projects.
    /// </summary>
    IReadOnlyList<RecentProject> RecentProjects { get; }

    /// <summary>
    /// Loads recent projects from the settings file.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Saves recent projects to the settings file.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Adds or updates a project in the recent list.
    /// </summary>
    Task AddProjectAsync(string filePath, string? name = null);

    /// <summary>
    /// Removes a project from the recent list.
    /// </summary>
    Task RemoveProjectAsync(string filePath);

    /// <summary>
    /// Toggles the pinned state of a project.
    /// </summary>
    Task TogglePinnedAsync(string filePath);

    /// <summary>
    /// Sets whether to show the welcome screen on startup.
    /// </summary>
    Task SetShowWelcomeOnStartupAsync(bool show);

    /// <summary>
    /// Clears all recent projects.
    /// </summary>
    Task ClearAllAsync();
}
