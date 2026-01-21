using System;
using System.IO;
using System.Text.Json;

namespace MusicEngineEditor.Models;

/// <summary>
/// Information about a MusicEngine project.
/// </summary>
public class ProjectInfo
{
    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full path to the project file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author name.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date the project was created.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the date the project was last modified.
    /// </summary>
    public DateTime ModifiedDate { get; set; }

    /// <summary>
    /// Gets or sets the project BPM.
    /// </summary>
    public double Bpm { get; set; } = 120.0;

    /// <summary>
    /// Gets or sets the number of patterns in the project.
    /// </summary>
    public int PatternCount { get; set; }

    /// <summary>
    /// Gets or sets the number of channels/tracks in the project.
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    /// Gets or sets the project duration in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets tags for categorization.
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets whether this is a favorite project.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Gets or sets the path to a preview image (if any).
    /// </summary>
    public string? PreviewImagePath { get; set; }

    /// <summary>
    /// Gets the file name without path.
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the directory containing the project.
    /// </summary>
    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets a formatted file size string.
    /// </summary>
    public string FileSizeFormatted
    {
        get
        {
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }

    /// <summary>
    /// Gets a formatted duration string.
    /// </summary>
    public string DurationFormatted
    {
        get
        {
            var ts = TimeSpan.FromSeconds(DurationSeconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }
    }

    /// <summary>
    /// Gets a relative time string for the modified date.
    /// </summary>
    public string ModifiedRelative
    {
        get
        {
            var diff = DateTime.Now - ModifiedDate;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)}w ago";
            return ModifiedDate.ToString("MMM d, yyyy");
        }
    }

    /// <summary>
    /// Creates a ProjectInfo from a file path.
    /// </summary>
    public static ProjectInfo FromFile(string filePath)
    {
        var info = new ProjectInfo
        {
            FilePath = filePath,
            Name = Path.GetFileNameWithoutExtension(filePath)
        };

        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            info.FileSize = fileInfo.Length;
            info.CreatedDate = fileInfo.CreationTime;
            info.ModifiedDate = fileInfo.LastWriteTime;

            // Try to read project metadata from JSON
            try
            {
                var json = File.ReadAllText(filePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("Name", out var nameProp))
                    info.Name = nameProp.GetString() ?? info.Name;
                if (root.TryGetProperty("Description", out var descProp))
                    info.Description = descProp.GetString() ?? string.Empty;
                if (root.TryGetProperty("Author", out var authorProp))
                    info.Author = authorProp.GetString() ?? string.Empty;
                if (root.TryGetProperty("BPM", out var bpmProp))
                    info.Bpm = bpmProp.GetDouble();
                if (root.TryGetProperty("Patterns", out var patternsProp) && patternsProp.ValueKind == JsonValueKind.Array)
                    info.PatternCount = patternsProp.GetArrayLength();
                if (root.TryGetProperty("Channels", out var channelsProp) && channelsProp.ValueKind == JsonValueKind.Array)
                    info.ChannelCount = channelsProp.GetArrayLength();
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        return info;
    }
}

/// <summary>
/// Sort options for the project browser.
/// </summary>
public enum ProjectSortOption
{
    /// <summary>Sort by name (A-Z)</summary>
    Name,
    /// <summary>Sort by name (Z-A)</summary>
    NameDescending,
    /// <summary>Sort by date modified (newest first)</summary>
    DateModified,
    /// <summary>Sort by date modified (oldest first)</summary>
    DateModifiedAscending,
    /// <summary>Sort by date created (newest first)</summary>
    DateCreated,
    /// <summary>Sort by file size (largest first)</summary>
    FileSize,
    /// <summary>Sort by BPM</summary>
    Bpm
}

/// <summary>
/// View mode for the project browser.
/// </summary>
public enum ProjectViewMode
{
    /// <summary>List view with details</summary>
    List,
    /// <summary>Grid view with thumbnails</summary>
    Grid,
    /// <summary>Compact list view</summary>
    Compact
}
